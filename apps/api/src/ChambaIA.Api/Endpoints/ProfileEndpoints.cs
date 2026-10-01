using ChambaIA.Api.Contracts;
using ChambaIA.Api.Infrastructure;
using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Skills;
using ChambaIA.Domain.Text;
using ChambaIA.Infrastructure.Identity;
using ChambaIA.Infrastructure.Matching;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ChambaIA.Api.Endpoints;

public static class ProfileEndpoints
{
    public static RouteGroupBuilder MapProfile(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/profile").WithTags("Profile").RequireAuthorization();

        group.MapGet("", async (HttpContext http, AppDbContext db, UserManager<ApplicationUser> users, CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            var user = await users.FindByIdAsync(userId.ToString());
            var profile = await db.CandidateProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, ct);
            return user is null || profile is null ? Results.NotFound() : Results.Ok(ToDto(user, profile));
        });

        group.MapPut("", async (UpdateProfileRequest request, HttpContext http, AppDbContext db,
                UserManager<ApplicationUser> users, MatchRecomputeService matcher, CancellationToken ct) =>
            {
                var userId = http.User.GetUserId();
                var user = await users.FindByIdAsync(userId.ToString());
                var profile = await db.CandidateProfiles.SingleOrDefaultAsync(p => p.UserId == userId, ct);
                if (user is null || profile is null) return Results.NotFound();

                user.FullName = request.FullName.Trim();
                await users.UpdateAsync(user);

                profile.Headline = request.Headline?.Trim();
                profile.ExperienceMonths = request.ExperienceMonths;
                profile.EducationLevel = request.EducationLevel;
                profile.EducationStatus = request.EducationStatus;
                profile.Skills = NormalizeSkills(request.Skills);
                profile.Languages = request.Languages.Select(l => new LanguageSkill { Name = l.Name.Trim(), Level = l.Level.Trim() }).ToList();
                profile.Experience = request.Experience.Select(e => new WorkExperience
                {
                    Title = e.Title.Trim(), Company = e.Company.Trim(), StartDate = e.StartDate, EndDate = e.EndDate, Description = e.Description?.Trim()
                }).ToList();
                profile.Education = request.Education.Select(e => new EducationEntry
                {
                    Institution = e.Institution.Trim(), Degree = e.Degree.Trim(), Level = e.Level, Status = e.Status
                }).ToList();
                profile.Certifications = request.Certifications.Select(c => c.Trim()).Where(c => c.Length > 0).Distinct().ToList();
                profile.UpdatedAt = DateTimeOffset.UtcNow;
                if (request.CompleteOnboarding) profile.OnboardingCompletedAt ??= profile.UpdatedAt;

                await db.SaveChangesAsync(ct);
                await matcher.RecomputeForUserAsync(userId, ct);
                return Results.Ok(ToDto(user, profile));
            })
            .Validate<UpdateProfileRequest>();

        return api;
    }

    private static List<ProfileSkill> NormalizeSkills(IEnumerable<SkillDto> skills) =>
        skills
            .Select(s =>
            {
                // Known skills collapse to their canonical key; unknown ones keep a slug so the user can still list them.
                var known = SkillCatalog.Resolve(s.Name) ?? SkillCatalog.FindByKey(s.Key);
                var name = known?.Name ?? s.Name.Trim();
                var key = known?.Key ?? TextNormalizer.Normalize(name).Replace(' ', '-');
                return new ProfileSkill { Key = key, Name = name, Level = s.Level };
            })
            .Where(s => s.Key.Length > 0)
            .GroupBy(s => s.Key)
            .Select(g => g.OrderByDescending(s => s.Level).First())
            .ToList();

    private static ProfileDto ToDto(ApplicationUser user, CandidateProfile p) => new(
        user.FullName, user.Email ?? "", p.Headline, p.ExperienceMonths, p.EducationLevel, p.EducationStatus,
        p.Skills.Select(s => new SkillDto(s.Key, s.Name, s.Level)).ToList(),
        p.Languages.Select(l => new LanguageDto(l.Name, l.Level)).ToList(),
        p.Experience.Select(e => new ExperienceDto(e.Title, e.Company, e.StartDate, e.EndDate, e.Description)).ToList(),
        p.Education.Select(e => new EducationDto(e.Institution, e.Degree, e.Level, e.Status)).ToList(),
        p.Certifications, p.OnboardingCompletedAt is not null, p.UpdatedAt);
}
