using ChambaIA.Domain.Geo;
using ChambaIA.Domain.Skills;
using ChambaIA.Domain.Text;

namespace ChambaIA.Tests.Domain;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("  Asistente Administrativa  ", "asistente administrativa")]
    [InlineData("Atención al Cliente!!", "atencion al cliente")]
    [InlineData("Ñandú, Perú", "nandu peru")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void Normalize_removes_accents_punctuation_and_case(string? input, string expected) =>
        Assert.Equal(expected, TextNormalizer.Normalize(input));

    [Fact]
    public void Stems_treat_gender_number_and_auxiliar_asistente_as_equal()
    {
        Assert.Equal(TextNormalizer.Stems("Asistente Administrativo"), TextNormalizer.Stems("Auxiliar Administrativa"));
        Assert.Equal(TextNormalizer.Stems("Asistentes Administrativas"), TextNormalizer.Stems("Asistente Administrativo"));
    }

    [Fact]
    public void ContainsPhrase_matches_whole_words_only()
    {
        Assert.True(TextNormalizer.ContainsPhrase("Ejecutivo de Call Center nocturno", "call center"));
        Assert.False(TextNormalizer.ContainsPhrase("Operador callejero", "call"));
    }

    [Fact]
    public void ContentHash_is_stable_across_formatting_and_changes_with_content()
    {
        var a = TextNormalizer.ContentHash("Asistente Administrativa", "Clínica Santa Aurora", "San Miguel", "Atención en ventanilla.");
        var b = TextNormalizer.ContentHash("  asistente administrativa ", "CLINICA SANTA AURORA", "san miguel", "Atención en ventanilla");
        var c = TextNormalizer.ContentHash("Asistente Administrativa", "Clínica Santa Aurora", "Comas", "Atención en ventanilla.");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal(64, a.Length);
    }
}

public class SkillCatalogTests
{
    [Theory]
    [InlineData("Atención al usuario", "atencion-al-cliente")]
    [InlineData("servicio al cliente", "atencion-al-cliente")]
    [InlineData("Facturación electrónica", "facturacion")]
    [InlineData("MS Excel", "excel")]
    [InlineData("google workspace", "google-drive")]
    public void Resolve_maps_aliases_to_canonical_keys(string text, string key) =>
        Assert.Equal(key, SkillCatalog.Resolve(text)?.Key);

    [Fact]
    public void Resolve_returns_null_for_unknown_skills() => Assert.Null(SkillCatalog.Resolve("Soldadura TIG"));

    [Fact]
    public void DetectIn_finds_skills_mentioned_in_free_text()
    {
        var found = SkillCatalog.DetectIn("Experiencia en atención al público, facturación electrónica y manejo de Excel y Word.")
            .Select(s => s.Key).ToHashSet();

        Assert.Contains("atencion-al-cliente", found);
        Assert.Contains("facturacion", found);
        Assert.Contains("excel", found);
        Assert.Contains("word", found);
    }

    [Fact]
    public void Catalog_keys_and_aliases_are_unambiguous()
    {
        Assert.Equal(SkillCatalog.All.Count, SkillCatalog.All.Select(s => s.Key).Distinct().Count());
        foreach (var skill in SkillCatalog.All)
            Assert.Equal(skill.Key, SkillCatalog.Resolve(skill.Name)?.Key);
    }
}

public class GeoTests
{
    [Theory]
    [InlineData("los olivos", "Los Olivos")]
    [InlineData("SMP", "San Martín de Porres")]
    [InlineData("surco", "Santiago de Surco")]
    [InlineData("Cercado", "Cercado")] // unknown: kept as typed
    public void Canonical_resolves_aliases_and_keeps_unknown_names(string input, string expected) =>
        Assert.Equal(expected, LimaDistricts.Canonical(input));

    [Fact]
    public void All_districts_are_unique_and_inside_metropolitan_lima()
    {
        Assert.Equal(LimaDistricts.All.Count, LimaDistricts.All.Select(d => d.Name).Distinct().Count());
        Assert.All(LimaDistricts.All, d =>
        {
            Assert.InRange(d.Latitude, -12.30, -11.80);
            Assert.InRange(d.Longitude, -77.20, -76.85);
        });
    }

    [Fact]
    public void Commute_is_short_between_neighbours_and_long_across_the_city()
    {
        var olivos = LimaDistricts.Find("Los Olivos")!;
        var comas = LimaDistricts.Find("Comas")!;
        var miraflores = LimaDistricts.Find("Miraflores")!;

        var near = CommuteEstimator.EstimateMinutes(olivos, comas.Latitude, comas.Longitude)!.Value;
        var far = CommuteEstimator.EstimateMinutes(olivos, miraflores.Latitude, miraflores.Longitude)!.Value;

        Assert.InRange(near, 10, 35);
        Assert.InRange(far, 60, 100);
        Assert.True(far > near);
    }
}
