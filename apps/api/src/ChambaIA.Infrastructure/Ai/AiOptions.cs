using ChambaIA.Domain.Ai;
using ChambaIA.Domain.Enums;

namespace ChambaIA.Infrastructure.Ai;

public sealed class AiProviderOptions
{
    /// <summary>"Ollama" (local model, free), "OpenAICompatible" (OpenAI, DeepSeek, Gemini's compatible endpoint, Groq, OpenRouter...) or "Anthropic".</summary>
    public string Type { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    /// <summary>Environment variable only (Ai__Providers__{name}__ApiKey). Never committed, never logged.</summary>
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "";
    /// <summary>USD per million tokens, copied by you from the provider's current price list. 0 for a local model.</summary>
    public decimal PricePerMillionInput { get; set; }
    public decimal PricePerMillionOutput { get; set; }
    /// <summary>0 = use the global Ai:TimeoutSeconds. A local model needs far more on its first call (it loads into memory): 120 is sensible.</summary>
    public int TimeoutSeconds { get; set; }
}

public sealed class AiScopeBudget
{
    public int DailyCalls { get; set; }
    public decimal MonthlyUsd { get; set; }

    public AiBudgetLimits ToLimits() => new(DailyCalls, MonthlyUsd);
}

public sealed class AiBudgetOptions
{
    /// <summary>Hard ceiling for everything the app spends on AI in a calendar month. Checked before every call.</summary>
    public decimal GlobalMonthlyUsd { get; set; } = 5m;
    /// <summary>Background work done by the system itself (offer extraction): not tied to a user.</summary>
    public AiScopeBudget System { get; set; } = new() { DailyCalls = 200, MonthlyUsd = 2m };
    public AiScopeBudget Free { get; set; } = new() { DailyCalls = 3, MonthlyUsd = 0.05m };
    public AiScopeBudget Pro { get; set; } = new() { DailyCalls = 20, MonthlyUsd = 1m };
    public AiScopeBudget ProPlus { get; set; } = new() { DailyCalls = 60, MonthlyUsd = 3m };

    public AiBudgetLimits For(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.Pro => Pro.ToLimits(),
        SubscriptionPlan.ProPlus => ProPlus.ToLimits(),
        _ => Free.ToLimits()
    };
}

public sealed class AiOptions
{
    public const string Section = "Ai";

    /// <summary>Master switch. Off by default: the product runs exactly the same without any model.</summary>
    public bool Enabled { get; set; }
    public int UtcOffsetHours { get; set; } = -5;
    public int TimeoutSeconds { get; set; } = 30;
    public int FailuresBeforeOpen { get; set; } = 3;
    public int OpenForSeconds { get; set; } = 120;
    /// <summary>How many offers one run of the enrichment job may send to a model.</summary>
    public int EnrichmentBatchSize { get; set; } = 20;

    public Dictionary<string, AiProviderOptions> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Task name → provider names in order of preference, e.g. JobExtraction: ["local", "cheap"].</summary>
    public Dictionary<string, string[]> Routes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public AiBudgetOptions Budgets { get; set; } = new();

    /// <summary>The configured providers of a task that actually exist, in order.</summary>
    public IReadOnlyList<string> RouteFor(AiTask task) =>
        Routes.TryGetValue(task.ToString(), out var names)
            ? names.Where(n => Providers.ContainsKey(n)).ToList()
            : [];
}
