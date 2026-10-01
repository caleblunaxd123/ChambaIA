using ChambaIA.Api.Contracts;
using ChambaIA.Api.Infrastructure;
using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Geo;
using ChambaIA.Infrastructure.Matching;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChambaIA.Api.Endpoints;

public static class PreferencesEndpoints
{
    public static RouteGroupBuilder MapPreferences(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/preferences").WithTags("Preferences").RequireAuthorization();

        group.MapGet("", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            var prefs = await db.JobPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, ct);
            return prefs is null ? Results.NotFound() : Results.Ok(ToDto(prefs));
        });

        group.MapPut("", async (UpdatePreferencesRequest request, HttpContext http, AppDbContext db,
                MatchRecomputeService matcher, CancellationToken ct) =>
            {
                var userId = http.User.GetUserId();
                var prefs = await db.JobPreferences.SingleOrDefaultAsync(p => p.UserId == userId, ct);
                if (prefs is null) return Results.NotFound();

                Apply(prefs, request);
                await db.SaveChangesAsync(ct);
                // Changing what you want changes what you see: the agent re-evaluates the feed right away.
                await matcher.RecomputeForUserAsync(userId, ct);
                return Results.Ok(ToDto(prefs));
            })
            .Validate<UpdatePreferencesRequest>();

        return api;
    }

    public static void Apply(JobPreferences prefs, UpdatePreferencesRequest r)
    {
        prefs.MinSalary = r.MinSalary;
        prefs.MaxSalary = r.MaxSalary;
        prefs.PreferredRoles = Clean(r.PreferredRoles);
        prefs.ExcludedRoles = Clean(r.ExcludedRoles);
        prefs.ExcludedKeywords = Clean(r.ExcludedKeywords);
        prefs.PreferredIndustries = Clean(r.PreferredIndustries);
        prefs.PreferredModalities = r.PreferredModalities.Distinct().ToList();
        prefs.EmploymentTypes = r.EmploymentTypes.Distinct().ToList();
        prefs.PreferredDistricts = Clean(r.PreferredDistricts.Select(LimaDistricts.Canonical));
        prefs.ExcludedDistricts = Clean(r.ExcludedDistricts.Select(LimaDistricts.Canonical));
        prefs.HomeDistrict = string.IsNullOrWhiteSpace(r.HomeDistrict) ? null : LimaDistricts.Canonical(r.HomeDistrict);
        prefs.MaxCommuteMinutes = r.MaxCommuteMinutes;
        prefs.WeekdaysOnly = r.WeekdaysOnly;
        prefs.MaxRequiredEducation = r.MaxRequiredEducation;
        prefs.NotificationFrequency = r.NotificationFrequency;
        prefs.PushEnabled = r.PushEnabled;
        prefs.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static PreferencesDto ToDto(JobPreferences p) => new(
        p.MinSalary, p.MaxSalary, p.PreferredRoles, p.ExcludedRoles, p.ExcludedKeywords, p.PreferredIndustries,
        p.PreferredModalities, p.EmploymentTypes, p.PreferredDistricts, p.ExcludedDistricts, p.HomeDistrict,
        p.MaxCommuteMinutes, p.WeekdaysOnly, p.MaxRequiredEducation, p.NotificationFrequency, p.PushEnabled, p.UpdatedAt);

    private static List<string> Clean(IEnumerable<string> items) =>
        items.Select(i => i.Trim()).Where(i => i.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
