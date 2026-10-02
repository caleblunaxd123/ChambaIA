using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Matching;
using ChambaIA.Infrastructure.Seeding;

namespace ChambaIA.Tests.Domain;

public class MatchExplainerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static CandidateProfile Areli => DemoCandidate.Profile(UserId);
    private static JobPreferences Prefs => DemoCandidate.Preferences(UserId);

    private static SkillRequirement Req(string key, string name, SkillLevel? level = null) => new() { Key = key, Name = name, MinLevel = level };

    private static JobOffer Job(params SkillRequirement[] skills) => new()
    {
        Title = "Asistente Administrativa",
        Company = "Clínica Santa Aurora",
        District = "San Miguel",
        Description = "Admisión y archivo.",
        SalaryMin = 1900,
        SalaryMax = 2200,
        ExperienceRequiredMinMonths = 24,
        SkillsRequired = [.. skills]
    };

    [Fact]
    public void A_perfect_fit_is_strong_on_every_axis_with_readable_notes()
    {
        var job = Job(Req("atencion-al-cliente", "Atención al cliente"), Req("gestion-documentaria", "Gestión documentaria"));

        var ex = MatchExplainer.Explain(Areli, Prefs, job, semanticScore: null);

        Assert.Equal(["role", "skills", "experience", "conditions"], ex.Dimensions.Select(d => d.Key));
        Assert.All(ex.Dimensions, d => Assert.Equal(DimensionLevel.Strong, d.Level));
        Assert.Contains("Tienes 2 de 2", ex.Dimensions.Single(d => d.Key == "skills").Note);
        Assert.Equal("Piden 2 años; tienes 2 años y 9 meses.", ex.Dimensions.Single(d => d.Key == "experience").Note);
        Assert.Contains("Coincide con", ex.Dimensions.Single(d => d.Key == "role").Note);
        Assert.Empty(ex.Improvements); // already excellent: nothing to suggest
    }

    [Fact]
    public void The_meaning_axis_only_exists_when_there_is_a_semantic_score()
    {
        var job = Job(Req("atencion-al-cliente", "Atención al cliente"));

        Assert.DoesNotContain(MatchExplainer.Explain(Areli, Prefs, job, null).Dimensions, d => d.Key == "meaning");

        var meaning = MatchExplainer.Explain(Areli, Prefs, job, 90).Dimensions.Single(d => d.Key == "meaning");
        Assert.Equal(DimensionLevel.Strong, meaning.Level);
        var weak = MatchExplainer.Explain(Areli, Prefs, job, 10).Dimensions.Single(d => d.Key == "meaning");
        Assert.Equal(DimensionLevel.Weak, weak.Level);
    }

    [Fact]
    public void A_missing_skill_weakens_the_skills_axis_and_says_so()
    {
        var job = Job(Req("sap", "SAP"), Req("cobranzas", "Cobranzas"), Req("compras", "Compras"));

        var skills = MatchExplainer.Explain(Areli, Prefs, job, null).Dimensions.Single(d => d.Key == "skills");

        Assert.Equal(DimensionLevel.Weak, skills.Level);
        Assert.Contains("Tienes 0 de 3", skills.Note);
    }

    [Fact]
    public void The_product_example_suggests_raising_excel_and_shows_the_result_honestly()
    {
        // "Solicitan Excel intermedio. Tu perfil indica nivel básico."
        var job = Job(Req("gestion-documentaria", "Gestión documentaria"), Req("excel", "Excel", SkillLevel.Intermediate));

        var ex = MatchExplainer.Explain(Areli, Prefs, job, null);

        var improvement = Assert.Single(ex.Improvements);
        Assert.Equal("Sube «Excel» a nivel intermedio en tu perfil", improvement.Title);
        Assert.Equal(MatchCategory.Excellent, improvement.ResultCategory);
        Assert.StartsWith("Solo si ya lo sabes hacer.", improvement.Detail);
        Assert.Contains("«Muy compatible» a «Excelente opción»", improvement.Detail);
    }

    [Fact]
    public void A_missing_skill_is_suggested_as_an_addition_never_as_a_given()
    {
        var job = Job(Req("atencion-al-cliente", "Atención al cliente"), Req("outlook", "Outlook"));

        var improvement = Assert.Single(MatchExplainer.Explain(Areli, Prefs, job, null).Improvements);

        Assert.Equal("Agrega «Outlook» a tu perfil", improvement.Title);
        Assert.Contains("Solo si ya lo sabes hacer", improvement.Detail);
    }

    [Fact]
    public void Several_missing_skills_can_be_suggested_together_when_that_is_what_unlocks_the_result()
    {
        var job = Job(Req("atencion-al-cliente", "Atención al cliente"), Req("sap", "SAP"), Req("outlook", "Outlook"));

        var improvements = MatchExplainer.Explain(Areli, Prefs, job, null).Improvements;

        Assert.Contains(improvements, i => i.Title == "Completa las 2 habilidades que te faltan" && i.ResultCategory == MatchCategory.Excellent);
        Assert.True(improvements.Count <= 3);
    }

    [Fact]
    public void Hard_filtered_offers_get_no_skill_advice()
    {
        var job = Job(Req("sap", "SAP"));
        job.SalaryMin = 900;
        job.SalaryMax = 1200; // below the candidate's minimum: skills cannot fix that

        var ex = MatchExplainer.Explain(Areli, Prefs, job, null);

        Assert.Empty(ex.Improvements);
        var conditions = ex.Dimensions.Single(d => d.Key == "conditions");
        Assert.Equal(DimensionLevel.Weak, conditions.Level);
        Assert.StartsWith("No cumple lo que pediste", conditions.Note);
    }

    [Fact]
    public void Explaining_never_changes_the_real_profile()
    {
        var profile = Areli;
        var before = profile.Skills.Select(s => (s.Key, s.Level)).ToList();

        MatchExplainer.Explain(profile, Prefs, Job(Req("excel", "Excel", SkillLevel.Advanced), Req("sap", "SAP")), null);

        Assert.Equal(before, profile.Skills.Select(s => (s.Key, s.Level)).ToList());
    }

    [Fact]
    public void The_explanation_agrees_with_the_engine_category()
    {
        var job = Job(Req("excel", "Excel", SkillLevel.Intermediate));
        var category = MatchEngine.Evaluate(Areli, Prefs, job).Category;

        var improvement = MatchExplainer.Explain(Areli, Prefs, job, null).Improvements.Single();

        Assert.True(improvement.ResultCategory > category);
    }
}
