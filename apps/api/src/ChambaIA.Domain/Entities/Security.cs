namespace ChambaIA.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    /// <summary>SHA-256 of the opaque token. The raw token is only ever sent to the client.</summary>
    public string TokenHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}

/// <summary>One row per AI call. Powers per-user/monthly budgets and the cost dashboard (phase 8).</summary>
public class AiUsage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? UserId { get; set; }
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string Operation { get; set; } = "";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal EstimatedCost { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
