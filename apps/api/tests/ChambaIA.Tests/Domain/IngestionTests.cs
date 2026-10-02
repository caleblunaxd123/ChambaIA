using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Ingestion;
using ChambaIA.Domain.Text;

namespace ChambaIA.Tests.Domain;

/// <summary>Phase 3: what the pipeline infers from a raw offer, and when two offers are the same job.</summary>
public class JobNormalizerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static RawJob Raw(string description = "Apoyo administrativo.", string title = "Asistente Administrativo", Func<RawJob, RawJob>? tweak = null)
    {
        var raw = new RawJob
        {
            ExternalId = "x-1",
            Title = title,
            Company = "Comercial Lima Norte S.A.C.",
            Description = description,
            Url = "https://empleos.example.com/x-1"
        };
        return tweak is null ? raw : tweak(raw);
    }

    private static NormalizedJob Norm(RawJob raw) => JobNormalizer.Normalize(raw, Now).Job ?? throw new Xunit.Sdk.XunitException(JobNormalizer.Normalize(raw, Now).Error);

    [Theory]
    [InlineData("S/ 1,800 - 2,200", 1800, 2200)]
    [InlineData("S/. 1800 a S/. 2500", 1800, 2500)]
    [InlineData("entre 2 300 y 2 600 soles", 2300, 2600)]
    [InlineData("S/ 2,500.00", 2500, null)]
    [InlineData("3000 soles", 3000, null)]
    [InlineData("Hasta S/ 2000", null, 2000)]
    public void Reads_salaries_written_the_peruvian_way(string text, int? min, int? max)
    {
        var job = Norm(Raw(tweak: r => r with { SalaryText = text }));
        Assert.Equal(min, (int?)job.SalaryMin);
        Assert.Equal(max, (int?)job.SalaryMax);
    }

    [Fact]
    public void Never_reads_years_schedules_or_phones_as_money()
    {
        var job = Norm(Raw("Experiencia desde 2023. Horario de 8:00 a 17:00. Llamar al 987654321. Ofrecemos S/ 1,900 mensuales."));
        Assert.Equal(1900, job.SalaryMin);
        Assert.Null(job.SalaryMax);

        var noCurrency = Norm(Raw("Empresa fundada en 1998 con 2500 colaboradores."));
        Assert.Null(noCurrency.SalaryMin);
    }

    [Theory]
    [InlineData("Requisitos: 2 años de experiencia en el puesto.", 24)]
    [InlineData("Experiencia mínima de 6 meses en atención al cliente.", 6)]
    [InlineData("Contar con un año de experiencia.", 12)]
    [InlineData("No requiere experiencia, te capacitamos.", 0)]
    [InlineData("Buscamos personas proactivas.", null)]
    public void Infers_required_experience(string description, int? months) =>
        Assert.Equal(months, Norm(Raw(description)).ExperienceMonths);

    [Theory]
    [InlineData("Estudios técnicos o universitarios en administración.", EducationLevel.Technical, false)]
    [InlineData("Bachiller en Contabilidad.", EducationLevel.University, true)]
    [InlineData("Estudiante universitario de últimos ciclos.", EducationLevel.University, false)]
    [InlineData("Secundaria completa.", EducationLevel.Secondary, true)]
    [InlineData("Ganas de aprender.", null, false)]
    public void Infers_the_lowest_accepted_education(string description, EducationLevel? level, bool completed)
    {
        var job = Norm(Raw(description));
        Assert.Equal(level, job.Education);
        Assert.Equal(completed, job.EducationCompleted);
    }

    [Fact]
    public void Infers_modality_schedule_and_contract_type()
    {
        Assert.Equal(WorkModality.Remote, Norm(Raw("Trabajo 100% remoto desde casa.")).Modality);
        Assert.Equal(WorkModality.Hybrid, Norm(Raw("Modalidad híbrida: 2 días remoto.")).Modality);
        Assert.Equal(WorkModality.OnSite, Norm(Raw("Trabajo en oficina.")).Modality);

        Assert.True(Norm(Raw("Horario de lunes a viernes.")).WeekdaysOnly);
        Assert.False(Norm(Raw("Horario de lunes a sábado.")).WeekdaysOnly);
        Assert.Null(Norm(Raw("Horario a coordinar.")).WeekdaysOnly);

        Assert.Equal(EmploymentType.PartTime, Norm(Raw(tweak: r => r with { EmploymentType = "Medio tiempo" })).EmploymentType);
        Assert.Equal(EmploymentType.Internship, Norm(Raw(title: "Practicante de Contabilidad")).EmploymentType);
        Assert.Equal(EmploymentType.FullTime, Norm(Raw()).EmploymentType);
    }

    [Fact]
    public void Finds_the_district_in_the_location_or_the_title()
    {
        Assert.Equal("Los Olivos", Norm(Raw(tweak: r => r with { Location = "Los Olivos, Lima, Perú" })).District);
        Assert.Equal("San Juan de Lurigancho", Norm(Raw(tweak: r => r with { Location = "SJL - Lima" })).District);
        Assert.Equal("San Isidro", Norm(Raw(title: "Cajero - San Isidro")).District);
        var arequipa = Norm(Raw(tweak: r => r with { Location = "Arequipa" }));
        Assert.Null(arequipa.District);
        Assert.Equal("Arequipa", arequipa.City);
    }

    [Fact]
    public void Splits_required_and_nice_to_have_skills_and_reads_levels()
    {
        var job = Norm(Raw("Funciones: emisión de facturas y atención al cliente. Requisito: Excel intermedio. Deseable conocimiento de SAP."));

        var required = job.SkillsRequired.ToDictionary(s => s.Key);
        Assert.Contains("facturacion", required.Keys);
        Assert.Contains("atencion-al-cliente", required.Keys);
        Assert.Equal(SkillLevel.Intermediate, required["excel"].MinLevel);
        Assert.Contains(job.SkillsPreferred, s => s.Key == "sap");
        Assert.DoesNotContain(job.SkillsRequired, s => s.Key == "sap");
    }

    [Fact]
    public void Health_and_public_service_wording_counts_as_customer_service()
    {
        Assert.Contains(Norm(Raw("Atención al paciente en admisión.")).SkillsRequired, s => s.Key == "atencion-al-cliente");
        Assert.Contains(Norm(Raw("Atención al ciudadano en ventanilla.")).SkillsRequired, s => s.Key == "atencion-al-cliente");
    }

    [Fact]
    public void Structured_fields_from_the_source_always_win()
    {
        var job = Norm(Raw("Sin experiencia. Trabajo remoto.", tweak: r => r with
        {
            ExperienceMonths = 18,
            Education = EducationLevel.University,
            EducationCompleted = true,
            SalaryMin = 2000,
            SkillsRequired = [new RawSkill("excel", SkillLevel.Advanced), new RawSkill("Soldadura TIG")]
        }));

        Assert.Equal(18, job.ExperienceMonths);
        Assert.Equal(EducationLevel.University, job.Education);
        Assert.Equal(2000, job.SalaryMin);
        Assert.Equal(SkillLevel.Advanced, job.SkillsRequired.Single(s => s.Key == "excel").MinLevel);
        Assert.Contains(job.SkillsRequired, s => s.Name == "Soldadura TIG");
    }

    [Fact]
    public void Cleans_html_and_keeps_paragraphs()
    {
        var job = Norm(Raw("<p>Funciones:</p><ul><li>Archivo &amp; despacho</li><li>Atención</li></ul><script>x</script>"));
        Assert.Equal("Funciones:\nArchivo & despacho\nAtención\nx", job.Description);
    }

    [Fact]
    public void Defaults_dates_and_never_accepts_the_future()
    {
        var job = Norm(Raw(tweak: r => r with { PostedAt = Now.AddDays(3) }));
        Assert.Equal(Now, job.PostedAt);
        Assert.Equal(Now + JobNormalizer.DefaultLifetime, job.ExpiresAt);
    }

    [Fact]
    public void Local_peruvian_times_are_stored_in_utc()
    {
        var lima = TimeSpan.FromHours(-5);
        var job = Norm(Raw(tweak: r => r with { PostedAt = new DateTimeOffset(2026, 10, 1, 6, 0, 0, lima), ExpiresAt = new DateTimeOffset(2026, 10, 30, 23, 0, 0, lima) }));
        Assert.Equal(TimeSpan.Zero, job.PostedAt.Offset);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 11, 0, 0, TimeSpan.Zero), job.PostedAt);
        Assert.Equal(TimeSpan.Zero, job.ExpiresAt.Offset);
    }

    [Theory]
    [InlineData("", "Empresa", "https://x.pe/1")]
    [InlineData("Título", "", "https://x.pe/1")]
    [InlineData("Título", "Empresa", "javascript:alert(1)")]
    [InlineData("Título", "Empresa", "no-es-url")]
    public void Rejects_offers_that_cannot_be_shown_or_applied_to(string title, string company, string url)
    {
        var result = JobNormalizer.Normalize(Raw(title: title, tweak: r => r with { Company = company, Url = url }), Now);
        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void The_content_hash_ignores_case_accents_and_spacing()
    {
        var a = Norm(Raw("Atención  al cliente.", title: "ASISTENTE ADMINISTRATIVO"));
        var b = Norm(Raw("atencion al cliente", title: "Asistente administrativo"));
        Assert.Equal(a.ContentHash, b.ContentHash);
    }
}

public class DuplicateDetectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static OfferFingerprint Offer(string title = "Asistente Administrativa", string company = "Clínica Santa Aurora", string? district = "San Miguel",
        decimal? min = 1900, decimal? max = 2200, int daysAgo = 0) =>
        new(TextNormalizer.Normalize(company), title, district, min, max, Now.AddDays(-daysAgo));

    [Fact]
    public void The_same_role_reworded_by_the_same_company_is_one_offer()
    {
        Assert.True(DuplicateDetector.LooksLikeSameOffer(Offer(), Offer(title: "Auxiliar Administrativo", min: 2000, max: null, daysAgo: 3)));
        Assert.True(DuplicateDetector.LooksLikeSameOffer(Offer(), Offer(district: null, min: null, max: null)));
    }

    [Fact]
    public void Different_company_district_role_salary_or_date_means_different_offers()
    {
        Assert.False(DuplicateDetector.LooksLikeSameOffer(Offer(), Offer(company: "Clínica San Pablo")));
        Assert.False(DuplicateDetector.LooksLikeSameOffer(Offer(), Offer(district: "Surco")));
        Assert.False(DuplicateDetector.LooksLikeSameOffer(Offer(), Offer(title: "Asistente de Facturación")));
        Assert.False(DuplicateDetector.LooksLikeSameOffer(Offer(), Offer(min: 3000, max: 3500)));
        Assert.False(DuplicateDetector.LooksLikeSameOffer(Offer(), Offer(daysAgo: 40)));
    }

    [Fact]
    public void Title_similarity_uses_stems_and_synonyms()
    {
        Assert.Equal(1, DuplicateDetector.TitleSimilarity("Auxiliar Administrativo", "Asistente Administrativa"));
        Assert.Equal(0, DuplicateDetector.TitleSimilarity("Cajero", "Asistente Administrativa"));
    }
}
