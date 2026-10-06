using ChambaIA.Domain.Ai;
using ChambaIA.Domain.Entities;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChambaIA.Infrastructure.Ai;

public sealed record PrepPreview(
    bool Available,
    string? UnavailableReason,
    string Payload,
    IReadOnlyList<string> Includes,
    IReadOnlyList<string> Excludes,
    AiQuota Quota);

public enum PrepOutcome { Ok, NotFound, NotAvailable, QuotaReached, Unreliable }

public sealed record PrepResult(PrepOutcome Outcome, PrepDraft? Draft = null);

/// <summary>
/// "Prepare my application": a premium, user-requested action. It is two steps on purpose. The preview shows the exact text that would
/// be sent to a model and the remaining quota; only a second, explicit call generates the draft. The draft is shown to the user and
/// stored nowhere: nothing is ever sent to an employer on anyone's behalf.
/// </summary>
public sealed class ApplicationPrepService(AppDbContext db, AiRouter router, TimeProvider clock)
{
    public static readonly string[] Includes =
    [
        "El cargo, la empresa y la descripción de la oferta (texto público)",
        "Tu nombre de pila, tu titular y tu experiencia total",
        "Tus habilidades con su nivel y tus últimos puestos (cargo, empresa y duración)",
        "Tus estudios (solo el nivel)"
    ];

    public static readonly string[] Excludes =
    [
        "Tu apellido, correo, teléfono y dirección",
        "Tu CV como archivo y cualquier documento de identidad",
        "Tus notas, tus postulaciones y tus preferencias"
    ];

    public async Task<PrepPreview?> PreviewAsync(Guid userId, Guid jobId, CancellationToken ct = default)
    {
        var context = await LoadAsync(userId, jobId, ct);
        if (context is null) return null;

        var payload = ApplicationPrepPrompt.User(context.Job.Title, context.Job.Company, context.Job.Description, context.Candidate);
        var available = router.CanRoute(AiTask.Premium);
        return new PrepPreview(
            available,
            available ? null : "La preparación con IA no está activada en este servidor.",
            payload, Includes, Excludes,
            await router.GetQuotaAsync(userId, context.Plan, AiTask.Premium, ct));
    }

    public async Task<PrepResult> GenerateAsync(Guid userId, Guid jobId, bool formal, CancellationToken ct = default)
    {
        var context = await LoadAsync(userId, jobId, ct);
        if (context is null) return new PrepResult(PrepOutcome.NotFound);
        if (!router.CanRoute(AiTask.Premium)) return new PrepResult(PrepOutcome.NotAvailable);

        var user = ApplicationPrepPrompt.User(context.Job.Title, context.Job.Company, context.Job.Description, context.Candidate);
        var answer = await router.CompleteAsync(
            AiTask.Premium, new AiCall(userId, context.Plan, ApplicationPrepPrompt.System(formal), user, MaxOutputTokens: 700), ct);

        if (!answer.Ok)
            return new PrepResult(answer.Reason == AiUnavailable.BudgetReached ? PrepOutcome.QuotaReached : PrepOutcome.NotAvailable);

        var draft = ApplicationPrepParser.Parse(answer.Text, context.Candidate);
        return draft is null ? new PrepResult(PrepOutcome.Unreliable) : new PrepResult(PrepOutcome.Ok, draft);
    }

    private sealed record Context(JobOffer Job, PrepCandidate Candidate, Domain.Enums.SubscriptionPlan Plan);

    private async Task<Context?> LoadAsync(Guid userId, Guid jobId, CancellationToken ct)
    {
        var job = await db.JobOffers.AsNoTracking().SingleOrDefaultAsync(j => j.Id == jobId, ct);
        var profile = await db.CandidateProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, ct);
        var account = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.FullName, u.Plan }).SingleOrDefaultAsync(ct);
        if (job is null || profile is null || account is null) return null;

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var experience = profile.Experience
            .OrderByDescending(e => e.StartDate ?? DateOnly.MinValue)
            .Take(3)
            .Select(e => new PrepExperience(e.Title, e.Company, MonthsBetween(e.StartDate, e.EndDate ?? today)))
            .ToList();

        var candidate = new PrepCandidate(
            FirstName(account.FullName), profile.Headline, profile.ExperienceMonths, profile.EducationLevel,
            profile.Skills.Take(15).Select(s => new PrepSkill(s.Key, s.Name, s.Level)).ToList(), experience);
        return new Context(job, candidate, account.Plan);
    }

    private static string FirstName(string? fullName) => (fullName ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

    private static int MonthsBetween(DateOnly? start, DateOnly end) =>
        start is null ? 0 : Math.Max(0, (end.Year - start.Value.Year) * 12 + end.Month - start.Value.Month);
}
