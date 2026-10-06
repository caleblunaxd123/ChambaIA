using System.Collections.Concurrent;
using ChambaIA.Domain.Ai;
using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;
using ChambaIA.Infrastructure.Embeddings;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChambaIA.Infrastructure.Ai;

public enum AiUnavailable { None, Disabled, NoRoute, BudgetReached, ProvidersFailed }

/// <summary>The outcome of one routed call. <see cref="Ok"/> false is a normal, expected result: callers carry on without AI.</summary>
public sealed record AiResult(bool Ok, string? Text, string? Provider, string? Model, AiUnavailable Reason, AiBudgetDecision Budget = AiBudgetDecision.Allowed)
{
    public static AiResult Unavailable(AiUnavailable reason, AiBudgetDecision budget = AiBudgetDecision.Allowed) => new(false, null, null, null, reason, budget);
}

/// <summary>Who is asking and what. <see cref="UserId"/> null means the system itself (background work), which has its own budget.</summary>
public sealed record AiCall(Guid? UserId, SubscriptionPlan Plan, string System, string User, int MaxOutputTokens = 500, bool Json = true);

/// <summary>What a scope may still do today and this month. Shown to people before they spend an AI action.</summary>
public sealed record AiQuota(int CallsToday, int DailyLimit, decimal SpentThisMonthUsd, decimal MonthlyLimitUsd)
{
    public int RemainingToday => Math.Max(0, DailyLimit - CallsToday);
}

/// <summary>One circuit breaker per provider, shared by every request and job in the process.</summary>
public sealed class AiCircuits(TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, EmbeddingCircuit> _circuits = new(StringComparer.OrdinalIgnoreCase);

    public EmbeddingCircuit For(string provider) => _circuits.GetOrAdd(provider, _ => new EmbeddingCircuit(clock));
}

/// <summary>
/// The only door to a language model. Business code asks for a <see cref="AiTask"/> and never names a provider:
/// 1) the master switch and the route for the task, 2) the budget (global cap, then user/system limits), 3) the providers of the
/// route in order, skipping any whose circuit is open, 4) every successful call is recorded in <c>AiUsage</c> with its estimated cost.
/// When nothing can answer, the result says why and the product simply continues without AI.
/// </summary>
public sealed class AiRouter(
    AppDbContext db,
    IAiProviderResolver providers,
    AiCircuits circuits,
    IOptions<AiOptions> options,
    TimeProvider clock,
    ILogger<AiRouter> logger)
{
    private readonly AiOptions _o = options.Value;

    /// <summary>True when AI is on and at least one provider of the task's route exists (it may still be refused for budget or health).</summary>
    public bool CanRoute(AiTask task) => _o.Enabled && _o.RouteFor(task).Count > 0;

    public async Task<AiResult> CompleteAsync(AiTask task, AiCall call, CancellationToken ct = default)
    {
        if (!_o.Enabled) return AiResult.Unavailable(AiUnavailable.Disabled);
        var route = _o.RouteFor(task);
        if (route.Count == 0) return AiResult.Unavailable(AiUnavailable.NoRoute);

        var budget = await CheckBudgetAsync(task, call, ct);
        if (budget != AiBudgetDecision.Allowed)
        {
            logger.LogInformation("AI call for {Task} refused by the budget: {Decision}.", task, budget);
            return AiResult.Unavailable(AiUnavailable.BudgetReached, budget);
        }

        var request = new AiRequest(call.System, call.User, call.MaxOutputTokens, call.Json);
        foreach (var name in route)
        {
            var circuit = circuits.For(name);
            if (circuit.IsOpen) continue;
            if (providers.Get(name) is not { } provider) continue;

            try
            {
                var response = await provider.CompleteAsync(request, ct);
                circuit.Success();
                await RecordAsync(name, provider.Model, task, call, response, ct);
                return new AiResult(true, response.Text, name, provider.Model, AiUnavailable.None);
            }
            catch (Exception ex) when (ex is AiProviderException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                if (circuit.Failure(_o.FailuresBeforeOpen, TimeSpan.FromSeconds(_o.OpenForSeconds)))
                    logger.LogWarning("AI provider {Provider} failed repeatedly; pausing it for {Seconds}s.", name, _o.OpenForSeconds);
                else
                    logger.LogInformation("AI provider {Provider} failed for {Task} ({Error}); trying the next one.", name, task, ex.Message);
            }
        }

        return AiResult.Unavailable(AiUnavailable.ProvidersFailed);
    }

    public async Task<AiQuota> GetQuotaAsync(Guid? userId, SubscriptionPlan plan, AiTask task, CancellationToken ct = default)
    {
        var (dayStart, monthStart) = Windows(clock.GetUtcNow(), _o.UtcOffsetHours);
        var scope = db.AiUsages.AsNoTracking().Where(u => u.UserId == userId);
        var operation = task.ToString();
        var limits = userId is null ? _o.Budgets.System.ToLimits(task) : _o.Budgets.For(plan, task);
        return new AiQuota(
            await scope.CountAsync(u => u.Operation == operation && u.CreatedAt >= dayStart, ct),
            limits.DailyCalls,
            await scope.Where(u => u.CreatedAt >= monthStart).SumAsync(u => (decimal?)u.EstimatedCost, ct) ?? 0m,
            limits.MonthlyUsd);
    }

    private async Task<AiBudgetDecision> CheckBudgetAsync(AiTask task, AiCall call, CancellationToken ct)
    {
        var (dayStart, monthStart) = Windows(clock.GetUtcNow(), _o.UtcOffsetHours);
        var scope = db.AiUsages.AsNoTracking().Where(u => u.UserId == call.UserId);
        var operation = task.ToString();

        // Calls are counted per task (each has its own daily allowance); dollars are counted across all of them.
        var spend = new AiSpend(
            await scope.CountAsync(u => u.Operation == operation && u.CreatedAt >= dayStart, ct),
            await scope.Where(u => u.CreatedAt >= monthStart).SumAsync(u => (decimal?)u.EstimatedCost, ct) ?? 0m);
        var global = await db.AiUsages.AsNoTracking().Where(u => u.CreatedAt >= monthStart).SumAsync(u => (decimal?)u.EstimatedCost, ct) ?? 0m;

        var limits = call.UserId is null ? _o.Budgets.System.ToLimits(task) : _o.Budgets.For(call.Plan, task);
        return AiBudgetPolicy.Check(limits, spend, _o.Budgets.GlobalMonthlyUsd, global);
    }

    private async Task RecordAsync(string providerName, string model, AiTask task, AiCall call, AiResponse response, CancellationToken ct)
    {
        // Providers that do not report usage get a character-based estimate: better a rough cost than an invisible one.
        var input = response.InputTokens > 0 ? response.InputTokens : AiCost.EstimateTokens(call.System) + AiCost.EstimateTokens(call.User);
        var output = response.OutputTokens > 0 ? response.OutputTokens : AiCost.EstimateTokens(response.Text);
        var prices = _o.Providers[providerName];

        db.AiUsages.Add(new AiUsage
        {
            UserId = call.UserId,
            Provider = providerName,
            Model = model,
            Operation = task.ToString(),
            InputTokens = input,
            OutputTokens = output,
            EstimatedCost = AiCost.Estimate(input, output, prices.PricePerMillionInput, prices.PricePerMillionOutput),
            CreatedAt = clock.GetUtcNow()
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Start of the local day and month, in UTC. Budgets reset at local midnight, not at 7 pm the day before.</summary>
    public static (DateTimeOffset Day, DateTimeOffset Month) Windows(DateTimeOffset now, int utcOffsetHours)
    {
        var local = now.ToOffset(TimeSpan.FromHours(utcOffsetHours));
        var offset = TimeSpan.FromHours(utcOffsetHours);
        var day = new DateTimeOffset(local.Year, local.Month, local.Day, 0, 0, 0, offset).ToUniversalTime();
        var month = new DateTimeOffset(local.Year, local.Month, 1, 0, 0, 0, offset).ToUniversalTime();
        return (day, month);
    }
}
