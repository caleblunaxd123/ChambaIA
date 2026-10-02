using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Matching;
using ChambaIA.Infrastructure.Seeding;

namespace ChambaIA.Tests.Domain;

public class SemanticSimilarityTests
{
    [Theory]
    [InlineData(0.20, 0)]    // below the floor
    [InlineData(0.45, 0)]
    [InlineData(0.615, 50)]
    [InlineData(0.78, 100)]
    [InlineData(0.95, 100)]  // above the ceiling
    public void Cosine_is_stretched_into_a_0_100_score(double cosine, double expected) =>
        Assert.Equal(expected, SemanticSimilarity.ToScore(cosine));

    [Fact]
    public void Score_is_monotonic_and_nan_safe()
    {
        var scores = Enumerable.Range(0, 21).Select(i => SemanticSimilarity.ToScore(i / 20.0)).ToArray();
        Assert.Equal(scores.OrderBy(s => s), scores);
        Assert.Equal(0, SemanticSimilarity.ToScore(double.NaN));
    }

    [Fact]
    public void Cosine_of_identical_opposite_and_orthogonal_vectors()
    {
        float[] a = [1, 0, 0];
        Assert.Equal(1, SemanticSimilarity.Cosine(a, a), 6);
        Assert.Equal(-1, SemanticSimilarity.Cosine(a, [-1, 0, 0]), 6);
        Assert.Equal(0, SemanticSimilarity.Cosine(a, [0, 1, 0]), 6);
        Assert.True(double.IsNaN(SemanticSimilarity.Cosine(a, [1, 0])));       // dimension mismatch
        Assert.True(double.IsNaN(SemanticSimilarity.Cosine(a, [0, 0, 0])));    // zero vector
    }
}

public class EmbeddingTextTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Job_text_carries_title_requirements_and_description_and_is_bounded()
    {
        var job = new JobOffer
        {
            Title = "Asistente Administrativa",
            Company = "Clínica Santa Aurora",
            Industry = "Salud",
            Description = new string('x', 5000),
            SkillsRequired = [new SkillRequirement { Key = "excel", Name = "Excel" }]
        };

        var text = EmbeddingText.ForJob(job);

        Assert.StartsWith("Asistente Administrativa. Clínica Santa Aurora", text);
        Assert.Contains("Requisitos: Excel", text);
        Assert.Equal(EmbeddingText.MaxChars, text.Length);
    }

    [Fact]
    public void Profile_text_has_the_work_history_but_no_personal_data()
    {
        var profile = DemoCandidate.Profile(UserId);
        var text = EmbeddingText.ForProfile(profile, DemoCandidate.Preferences(UserId));

        Assert.Contains("Asistente administrativa", text);
        Assert.Contains("Facturación", text);
        Assert.Contains("Busco trabajo como: Asistente Administrativo", text);
        Assert.DoesNotContain("Areli", text);
        Assert.DoesNotContain("@", text);
    }

    [Fact]
    public void Hash_changes_with_the_text_and_with_the_model()
    {
        var a = EmbeddingText.Hash("hola mundo", "bge-m3");

        Assert.Equal(a, EmbeddingText.Hash("hola mundo", "bge-m3"));
        Assert.NotEqual(a, EmbeddingText.Hash("hola mundo!", "bge-m3"));
        Assert.NotEqual(a, EmbeddingText.Hash("hola mundo", "multilingual-e5"));
    }

    [Fact]
    public void An_empty_profile_is_not_worth_embedding()
    {
        var empty = new CandidateProfile { UserId = UserId };
        Assert.False(EmbeddingText.IsMeaningful(EmbeddingText.ForProfile(empty, new JobPreferences { UserId = UserId })));
        Assert.True(EmbeddingText.IsMeaningful(EmbeddingText.ForProfile(DemoCandidate.Profile(UserId), DemoCandidate.Preferences(UserId))));
    }
}

/// <summary>Stage C is additive: without a semantic score the engine is exactly what it was before embeddings existed.</summary>
public class MatchEngineSemanticTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly CandidateProfile Areli = DemoCandidate.Profile(UserId);
    private static readonly JobPreferences Prefs = DemoCandidate.Preferences(UserId);

    private static JobOffer Job(string title = "Auxiliar de Oficina") => new()
    {
        Title = title,
        Company = "Empresa Demo",
        District = "Los Olivos",
        Description = "Apoyo en oficina.",
        SalaryMin = 1900,
        SalaryMax = 2200,
        ExperienceRequiredMinMonths = 24,
        SkillsRequired = [new SkillRequirement { Key = "gestion-documentaria", Name = "Gestión documentaria" }]
    };

    [Fact]
    public void Without_a_semantic_score_nothing_changes()
    {
        var plain = MatchEngine.Evaluate(Areli, Prefs, Job());
        var explicitNull = MatchEngine.Evaluate(Areli, Prefs, Job(), null);

        Assert.Null(plain.SemanticScore);
        Assert.Equal(plain.OverallScore, explicitNull.OverallScore);
        Assert.Equal(plain.Category, explicitNull.Category);
    }

    [Fact]
    public void The_semantic_score_is_reported_and_moves_the_overall_score_in_its_direction()
    {
        var low = MatchEngine.Evaluate(Areli, Prefs, Job(), 10);
        var high = MatchEngine.Evaluate(Areli, Prefs, Job(), 95);

        Assert.Equal(10, low.SemanticScore);
        Assert.Equal(95, high.SemanticScore);
        Assert.True(high.OverallScore > low.OverallScore);
    }

    [Fact]
    public void A_title_that_reads_differently_but_means_the_same_is_not_capped_as_unrelated()
    {
        var job = Job("Encargado de Trámites Documentarios");
        var withoutMeaning = MatchEngine.Evaluate(Areli, Prefs, job, 5);
        var withMeaning = MatchEngine.Evaluate(Areli, Prefs, job, 90);

        Assert.True(withoutMeaning.Category <= MatchCategory.Compatible);
        Assert.True(withMeaning.Category > withoutMeaning.Category);
        Assert.Contains(withMeaning.Reasons, r => r.Code == "semantic-match");
        Assert.DoesNotContain(withoutMeaning.Reasons, r => r.Code == "semantic-match");
    }

    [Fact]
    public void Meaning_never_overrides_a_hard_rule()
    {
        var belowMinimum = Job();
        belowMinimum.SalaryMin = 1000;
        belowMinimum.SalaryMax = 1200;

        var outcome = MatchEngine.Evaluate(Areli, Prefs, belowMinimum, 100);

        Assert.True(outcome.IsHardFiltered);
        Assert.Equal(MatchCategory.Poor, outcome.Category);
    }

    [Fact]
    public void Meaning_never_hides_a_missing_requirement()
    {
        var job = Job();
        job.SkillsRequired = [new SkillRequirement { Key = "sap", Name = "SAP" }, new SkillRequirement { Key = "cobranzas", Name = "Cobranzas" }, new SkillRequirement { Key = "compras", Name = "Compras" }];

        var outcome = MatchEngine.Evaluate(Areli, Prefs, job, 100);

        Assert.True(outcome.Category <= MatchCategory.Review);
    }

    [Fact]
    public void The_score_stays_inside_its_category_band()
    {
        var outcome = MatchEngine.Evaluate(Areli, Prefs, Job("Mecánico Industrial"), 100);
        Assert.True(MatchEngine.Categorize(outcome.OverallScore) >= outcome.Category);
    }
}
