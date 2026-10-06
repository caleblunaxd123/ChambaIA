using ChambaIA.Domain.Ai;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChambaIA.Infrastructure.Ai;

public sealed record AiTotals(int Calls, long InputTokens, long OutputTokens, decimal CostUsd);

public sealed record AiGroup(string Key, int Calls, long InputTokens, long OutputTokens, decimal CostUsd);

public sealed record AiDay(DateOnly Date, int Calls, decimal CostUsd);

public sealed record AiBudgetStatus(
    decimal GlobalMonthlyUsd,
    decimal SpentThisMonthUsd,
    decimal RemainingUsd,
    int SystemCallsToday,
    int SystemDailyCallsLimit,
    decimal SystemSpentThisMonthUsd,
    decimal SystemMonthlyUsdLimit);

/// <summary>What the dashboard may say about a provider. The API key is never part of it, only whether one is set.</summary>
public sealed record AiProviderStatus(string Name, string Type, string Model, bool HasApiKey, bool CircuitOpen, IReadOnlyList<string> Tasks);

public sealed record AiUsageReport(
    bool Enabled,
    int Days,
    AiTotals Totals,
    IReadOnlyList<AiGroup> ByProvider,
    IReadOnlyList<AiGroup> ByOperation,
    IReadOnlyList<AiDay> ByDay,
    AiBudgetStatus Budget,
    IReadOnlyList<AiProviderStatus> Providers,
    int OffersPendingReview);

/// <summary>The cost dashboard: real numbers from <c>AiUsage</c>, never estimates made up in advance.</summary>
public sealed class AiUsageService(AppDbContext db, AiCircuits circuits, IOptions<AiOptions> options, TimeProvider clock)
{
    private readonly AiOptions _o = options.Value;

    public async Task<AiUsageReport> ReportAsync(int days, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, 366);
        var now = clock.GetUtcNow();
        var since = now.AddDays(-days);
        var (dayStart, monthStart) = AiRouter.Windows(now, _o.UtcOffsetHours);

        var period = db.AiUsages.AsNoTracking().Where(u => u.CreatedAt >= since);
        var totals = await period
            .GroupBy(_ => 1)
            .Select(g => new AiTotals(g.Count(), g.Sum(u => (long)u.InputTokens), g.Sum(u => (long)u.OutputTokens), g.Sum(u => u.EstimatedCost)))
            .FirstOrDefaultAsync(ct) ?? new AiTotals(0, 0, 0, 0m);

        var byProvider = (await period.GroupBy(u => u.Provider + " · " + u.Model)
            .Select(g => new AiGroup(g.Key, g.Count(), g.Sum(u => (long)u.InputTokens), g.Sum(u => (long)u.OutputTokens), g.Sum(u => u.EstimatedCost)))
            .ToListAsync(ct))
            .OrderByDescending(g => g.CostUsd).ThenByDescending(g => g.Calls).ToList();

        var byOperation = (await period.GroupBy(u => u.Operation)
            .Select(g => new AiGroup(g.Key, g.Count(), g.Sum(u => (long)u.InputTokens), g.Sum(u => (long)u.OutputTokens), g.Sum(u => u.EstimatedCost)))
            .ToListAsync(ct))
            .OrderByDescending(g => g.CostUsd).ThenByDescending(g => g.Calls).ToList();

        // Grouped in memory by local day: translating a time-zone-aware date to SQL is not worth the trouble for a few thousand rows.
        var offset = TimeSpan.FromHours(_o.UtcOffsetHours);
        var rows = await period.Select(u => new { u.CreatedAt, u.EstimatedCost }).ToListAsync(ct);
        var byDay = rows
            .GroupBy(r => DateOnly.FromDateTime(r.CreatedAt.ToOffset(offset).DateTime))
            .Select(g => new AiDay(g.Key, g.Count(), g.Sum(r => r.EstimatedCost)))
            .OrderBy(d => d.Date).ToList();

        var spentMonth = await db.AiUsages.AsNoTracking().Where(u => u.CreatedAt >= monthStart).SumAsync(u => (decimal?)u.EstimatedCost, ct) ?? 0m;
        var system = db.AiUsages.AsNoTracking().Where(u => u.UserId == null);
        var systemSpent = await system.Where(u => u.CreatedAt >= monthStart).SumAsync(u => (decimal?)u.EstimatedCost, ct) ?? 0m;
        var budget = new AiBudgetStatus(
            _o.Budgets.GlobalMonthlyUsd, spentMonth, Math.Max(0m, _o.Budgets.GlobalMonthlyUsd - spentMonth),
            await system.CountAsync(u => u.CreatedAt >= dayStart, ct), _o.Budgets.System.DailyCalls, systemSpent, _o.Budgets.System.MonthlyUsd);

        var providers = _o.Providers.Select(p => new AiProviderStatus(
            p.Key, p.Value.Type, p.Value.Model, !string.IsNullOrWhiteSpace(p.Value.ApiKey), circuits.For(p.Key).IsOpen,
            Enum.GetValues<AiTask>().Where(t => _o.RouteFor(t).Contains(p.Key, StringComparer.OrdinalIgnoreCase)).Select(t => t.ToString()).ToList()))
            .OrderBy(p => p.Name).ToList();

        var pending = await db.JobOffers.CountAsync(j => j.IsActive && j.DuplicateOfId == null && (j.AiExtractionHash == null || j.AiExtractionHash != j.ContentHash), ct);

        return new AiUsageReport(_o.Enabled, days, totals, byProvider, byOperation, byDay, budget, providers, pending);
    }
}
