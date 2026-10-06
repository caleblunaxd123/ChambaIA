namespace ChambaIA.Domain.Ai;

/// <summary>What the router can be asked to do. Each task has its own ordered list of providers in configuration.</summary>
public enum AiTask
{
    /// <summary>Fills the blanks of an offer the deterministic parser could not read (salary, experience, skills...).</summary>
    JobExtraction,
    /// <summary>Understands a command the rule-based agent did not recognise (phase 9).</summary>
    CommandFallback,
    /// <summary>User-requested premium actions: cover letter, adapted CV, interview prep (phase 9, paid plans).</summary>
    Premium
}

public sealed record AiBudgetLimits(int DailyCalls, decimal MonthlyUsd);

/// <summary>What was already spent in the current day (calls) and month (USD) for one scope: a user, or the system itself.</summary>
public sealed record AiSpend(int CallsToday, decimal CostThisMonth);

public enum AiBudgetDecision { Allowed, DailyCallsReached, MonthlyBudgetReached, GlobalBudgetReached }

/// <summary>
/// "Never get a surprise bill": a pure function from (limits, spend) to allowed / denied. The global cap is checked first so a
/// runaway can never exceed it no matter how generous the per-scope limits are. A limit of 0 means "none allowed".
/// </summary>
public static class AiBudgetPolicy
{
    public static AiBudgetDecision Check(AiBudgetLimits scopeLimits, AiSpend scopeSpend, decimal globalMonthlyUsd, decimal globalSpentThisMonth)
    {
        if (globalSpentThisMonth >= globalMonthlyUsd) return AiBudgetDecision.GlobalBudgetReached;
        if (scopeSpend.CallsToday >= scopeLimits.DailyCalls) return AiBudgetDecision.DailyCallsReached;
        if (scopeSpend.CostThisMonth >= scopeLimits.MonthlyUsd) return AiBudgetDecision.MonthlyBudgetReached;
        return AiBudgetDecision.Allowed;
    }
}

public static class AiCost
{
    /// <summary>Estimated cost in USD from token counts and the provider's price per million tokens.</summary>
    public static decimal Estimate(int inputTokens, int outputTokens, decimal pricePerMillionInput, decimal pricePerMillionOutput) =>
        (Math.Max(0, inputTokens) * pricePerMillionInput + Math.Max(0, outputTokens) * pricePerMillionOutput) / 1_000_000m;

    /// <summary>Rough token count for providers that do not report usage: about 4 characters per token in Spanish.</summary>
    public static int EstimateTokens(string? text) => string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;
}
