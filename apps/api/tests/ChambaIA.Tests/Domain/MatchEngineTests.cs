using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Matching;
using ChambaIA.Infrastructure.Seeding;

namespace ChambaIA.Tests.Domain;

/// <summary>The matching engine is the product: these tests pin down its contract with the demo candidate (Areli, 2 años 9 meses).</summary>
public class MatchEngineTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly CandidateProfile Areli = DemoCandidate.Profile(UserId);

    private static JobPreferences Prefs(Action<JobPreferences>? tweak = null)
    {
        var prefs = DemoCandidate.Preferences(UserId);
        tweak?.Invoke(prefs);
        return prefs;
    }

    private static SkillRequirement Req(string key, SkillLevel? level = null) => new() { Key = key, Name = key, MinLevel = level };

    private static JobOffer Job(Action<JobOffer>? tweak = null)
    {
        var job = new JobOffer
        {
            Title = "Asistente Administrativo",
            Company = "Empresa Demo",
            District = "Los Olivos",
            Description = "Apoyo administrativo.",
            SalaryMin = 1900,
            SalaryMax = 2200,
            ExperienceRequiredMinMonths = 24,
            SkillsRequired = [Req("gestion-documentaria"), Req("atencion-al-cliente")],
            WeekdaysOnly = true
        };
        tweak?.Invoke(job);
        return job;
    }

    private static MatchOutcome Evaluate(JobOffer job, JobPreferences? prefs = null) =>
        MatchEngine.Evaluate(Areli, prefs ?? Prefs(), job);

    [Fact]
    public void Perfect_fit_is_excellent_and_explains_why()
    {
        var outcome = Evaluate(Job());

        Assert.Equal(MatchCategory.Excellent, outcome.Category);
        Assert.False(outcome.IsHardFiltered);
        Assert.Empty(outcome.MissingSkills);
        Assert.Contains(outcome.Reasons, r => r.Code == "experience-ok" && r.Detail == "Tienes 2 años y 9 meses.");
        Assert.Contains(outcome.Reasons, r => r.Code == "skill-match" && r.Title == "Piden gestion-documentaria");
        Assert.Contains(outcome.Reasons, r => r.Code == "role-match");
    }

    [Fact]
    public void Skill_level_gap_is_a_warning_and_caps_the_category_at_very_compatible()
    {
        // The product example: "Solicitan Excel intermedio. Tu perfil indica nivel básico."
        var outcome = Evaluate(Job(j => j.SkillsRequired = [Req("gestion-documentaria"), Req("excel", SkillLevel.Intermediate)]));

        Assert.Equal(MatchCategory.VeryCompatible, outcome.Category);
        var warning = Assert.Single(outcome.Warnings, w => w.Code == "skill-level-gap");
        Assert.Equal("Solicitan excel intermedio", warning.Title);
        Assert.Equal("Tu perfil indica nivel básico.", warning.Detail);
    }

    [Fact]
    public void Missing_required_skills_lower_the_category_and_are_listed()
    {
        var outcome = Evaluate(Job(j => j.SkillsRequired = [Req("cobranzas"), Req("sap"), Req("contabilidad-basica")]));

        Assert.Equal(MatchCategory.Review, outcome.Category);
        Assert.Equal(["cobranzas", "sap", "contabilidad-basica"], outcome.MissingSkills);
        Assert.Equal(3, outcome.Warnings.Count(w => w.Code == "skill-missing"));
    }

    [Fact]
    public void Score_never_contradicts_the_category_band()
    {
        var capped = Evaluate(Job(j => j.SkillsRequired = [Req("excel", SkillLevel.Intermediate), Req("gestion-documentaria")]));

        Assert.Equal(MatchCategory.VeryCompatible, capped.Category);
        Assert.True(capped.OverallScore < 80);
    }

    [Fact]
    public void Salary_below_minimum_is_a_hard_filter()
    {
        var outcome = Evaluate(Job(j => { j.SalaryMin = 1200; j.SalaryMax = 1500; }));

        Assert.True(outcome.IsHardFiltered);
        Assert.Equal(MatchCategory.Poor, outcome.Category);
        Assert.Contains(outcome.Warnings, w => w.Code == "salary-below-min" && w.Detail == "Tu mínimo es S/ 1,800.");
        Assert.True(outcome.OverallScore <= 25);
    }

    [Fact]
    public void Range_that_reaches_the_minimum_is_not_filtered()
    {
        var outcome = Evaluate(Job(j => { j.SalaryMin = 1200; j.SalaryMax = 1800; }));
        Assert.False(outcome.IsHardFiltered);
    }

    [Fact]
    public void Unknown_salary_is_a_soft_warning_not_a_filter()
    {
        var outcome = Evaluate(Job(j => { j.SalaryMin = null; j.SalaryMax = null; }));

        Assert.False(outcome.IsHardFiltered);
        Assert.Contains(outcome.Warnings, w => w.Code == "salary-unknown");
    }

    [Fact]
    public void Excluded_district_is_a_hard_filter()
    {
        var outcome = Evaluate(Job(j => j.District = "Ate"), Prefs(p => p.ExcludedDistricts = ["Ate"]));

        Assert.True(outcome.IsHardFiltered);
        Assert.Contains(outcome.Warnings, w => w.Code == "excluded-district");
    }

    [Fact]
    public void Excluded_district_does_not_apply_to_remote_jobs()
    {
        var outcome = Evaluate(Job(j => { j.District = "Ate"; j.Modality = WorkModality.Remote; }), Prefs(p => p.ExcludedDistricts = ["Ate"]));
        Assert.False(outcome.IsHardFiltered);
    }

    [Fact]
    public void Excluded_keyword_in_description_is_a_hard_filter()
    {
        var job = Job(j => j.Description = "Trabajo en call center con turnos rotativos.");
        var outcome = Evaluate(job, Prefs(p => p.ExcludedKeywords = ["call center"]));

        Assert.True(outcome.IsHardFiltered);
        Assert.Contains(outcome.Warnings, w => w.Code == "excluded-keyword");
    }

    [Fact]
    public void Excluded_role_matches_the_title()
    {
        var outcome = Evaluate(Job(j => j.Title = "Cajera"), Prefs(p => p.ExcludedRoles = ["cajera"]));
        Assert.Contains(outcome.Warnings, w => w.Code == "excluded-role");
    }

    [Fact]
    public void Required_completed_university_is_filtered_when_the_candidate_excluded_it()
    {
        var outcome = Evaluate(Job(j => { j.EducationRequired = EducationLevel.University; j.EducationRequiredCompleted = true; }));

        Assert.True(outcome.IsHardFiltered);
        Assert.Contains(outcome.Warnings, w => w.Code == "education-above-ceiling");
    }

    [Fact]
    public void University_in_progress_is_acceptable_when_the_offer_does_not_demand_it_finished()
    {
        var outcome = Evaluate(Job(j => { j.EducationRequired = EducationLevel.University; j.EducationRequiredCompleted = false; }));

        Assert.False(outcome.IsHardFiltered);
        Assert.Contains(outcome.Reasons, r => r.Code == "education-ok");
    }

    [Theory]
    [InlineData(36, false)] // 3 años: Areli tiene 2 años 9 meses → cerca, solo advertencia
    [InlineData(60, true)]  // 5 años: demasiado
    public void Experience_far_above_the_candidate_is_filtered_but_a_small_gap_is_only_a_warning(int requiredMonths, bool filtered)
    {
        var outcome = Evaluate(Job(j => j.ExperienceRequiredMinMonths = requiredMonths));

        Assert.Equal(filtered, outcome.IsHardFiltered);
        if (!filtered) Assert.Contains(outcome.Warnings, w => w.Code == "experience-close");
    }

    [Fact]
    public void Commute_longer_than_the_limit_is_filtered_and_shorter_is_explained()
    {
        var limited = Prefs(p => p.MaxCommuteMinutes = 60);

        var far = Evaluate(Job(j => j.District = "Miraflores"), limited);
        var near = Evaluate(Job(j => j.District = "Comas"), limited);

        Assert.True(far.IsHardFiltered);
        Assert.Contains(far.Warnings, w => w.Code == "commute-too-long");
        Assert.False(near.IsHardFiltered);
        Assert.Contains(near.Reasons, r => r.Code == "commute-short");
    }

    [Fact]
    public void Weekdays_only_preference_filters_jobs_that_include_weekends()
    {
        var prefs = Prefs(p => p.WeekdaysOnly = true);

        Assert.True(Evaluate(Job(j => j.WeekdaysOnly = false), prefs).IsHardFiltered);
        Assert.False(Evaluate(Job(j => j.WeekdaysOnly = true), prefs).IsHardFiltered);
        Assert.False(Evaluate(Job(j => j.WeekdaysOnly = null), prefs).IsHardFiltered);
    }

    [Fact]
    public void Modality_preference_filters_other_modalities()
    {
        var onlyRemote = Prefs(p => p.PreferredModalities = [WorkModality.Remote]);

        Assert.True(Evaluate(Job(j => j.Modality = WorkModality.OnSite), onlyRemote).IsHardFiltered);
        Assert.False(Evaluate(Job(j => j.Modality = WorkModality.Remote), onlyRemote).IsHardFiltered);
    }

    [Fact]
    public void A_title_that_does_not_resemble_the_target_roles_is_never_top_ranked()
    {
        var outcome = Evaluate(Job(j => j.Title = "Mecánico Industrial"));
        Assert.True(outcome.Category <= MatchCategory.Compatible);
    }

    [Fact]
    public void Evaluation_is_deterministic()
    {
        var job = Job(j => j.SkillsRequired = [Req("excel", SkillLevel.Intermediate), Req("sap")]);
        var a = Evaluate(job);
        var b = Evaluate(job);

        Assert.Equal(a.OverallScore, b.OverallScore);
        Assert.Equal(a.Category, b.Category);
        Assert.Equal(a.Warnings.Select(w => w.Code), b.Warnings.Select(w => w.Code));
    }

    [Theory]
    [InlineData(33, "2 años y 9 meses")]
    [InlineData(12, "1 año")]
    [InlineData(24, "2 años")]
    [InlineData(1, "1 mes")]
    [InlineData(0, "sin experiencia")]
    public void Duration_is_written_in_natural_spanish(int months, string expected) =>
        Assert.Equal(expected, MatchFormatting.Duration(months));
}
