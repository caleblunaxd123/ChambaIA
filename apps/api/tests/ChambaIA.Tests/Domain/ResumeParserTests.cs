using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Resumes;

namespace ChambaIA.Tests.Domain;

public class ResumeParserTests
{
    private static readonly DateOnly Today = new(2026, 10, 1);

    public const string SampleCv = """
        ARELI QUISPE MAMANI
        Los Olivos, Lima · 999 999 999 · areli@example.com

        PERFIL PROFESIONAL
        Asistente administrativa con experiencia en atención al usuario, gestión documentaria y facturación. Orientada a resultados.

        EXPERIENCIA LABORAL
        Asistente Administrativa | Centro Educativo Santa Rosa
        Dic 2023 - Actualidad
        • Atención a padres de familia presencial, telefónica y por WhatsApp.
        • Archivo y gestión documentaria, emisión de facturas y boletas.
        • Coordinación de agenda y programación de actividades institucionales.

        Auxiliar de Atención al Usuario - Clínica San Pablo
        Abr 2022 - Nov 2023
        • Recepción de pacientes y registro de documentos.
        • Reportes semanales en Excel.

        EDUCACIÓN
        Instituto Cibertec - Técnico en Administración de Empresas (2019 - 2021) Egresada
        Universidad Autónoma - Administración de Empresas, 6.º ciclo (en curso)

        HABILIDADES
        Atención al cliente
        Gestión documentaria
        Facturación electrónica
        Excel básico
        Word intermedio
        Google Drive
        Trabajo bajo presión

        IDIOMAS
        Español - Nativo
        Inglés - Básico

        CURSOS
        • Excel básico - Cibertec (2022)
        • Atención al cliente - SENATI (2021)
        """;

    private static ParsedResume Parse(string text = SampleCv) => ResumeParser.Parse(text, Today);

    [Fact]
    public void Experience_entries_are_found_with_title_company_and_dates()
    {
        var cv = Parse();

        Assert.Equal(2, cv.Experience.Count);
        var current = cv.Experience[0];
        Assert.Equal("Asistente Administrativa", current.Title);
        Assert.Equal("Centro Educativo Santa Rosa", current.Company);
        Assert.Equal(new DateOnly(2023, 12, 1), current.StartDate);
        Assert.Null(current.EndDate);
        Assert.Contains("gestión documentaria", current.Description);

        var previous = cv.Experience[1];
        Assert.Equal("Auxiliar de Atención al Usuario", previous.Title);
        Assert.Equal("Clínica San Pablo", previous.Company);
        Assert.Equal(new DateOnly(2022, 4, 1), previous.StartDate);
        Assert.Equal(new DateOnly(2023, 11, 1), previous.EndDate);
    }

    [Fact]
    public void Total_experience_counts_months_inclusively()
    {
        // Abr 2022 – Nov 2023 = 20 meses; Dic 2023 – Oct 2026 = 35 meses.
        Assert.Equal(55, Parse().ExperienceMonths);
    }

    [Fact]
    public void Overlapping_jobs_are_counted_once()
    {
        const string cv = """
            EXPERIENCIA
            Asistente | Empresa A
            Ene 2022 - Dic 2022
            Auxiliar | Empresa B
            Jun 2022 - Dic 2023
            """;

        Assert.Equal(24, ResumeParser.Parse(cv, Today).ExperienceMonths);
    }

    [Fact]
    public void Highest_education_is_the_ongoing_university_degree_and_both_entries_are_kept()
    {
        var cv = Parse();

        Assert.Equal(EducationLevel.University, cv.EducationLevel);
        Assert.Equal(EducationStatus.InProgress, cv.EducationStatus);
        Assert.Contains(cv.Education, e => e.Level == EducationLevel.Technical && e.Status == EducationStatus.Completed && e.Institution.Contains("Cibertec"));
        Assert.Contains(cv.Education, e => e.Level == EducationLevel.University && e.Status == EducationStatus.InProgress);
    }

    [Fact]
    public void Skills_use_canonical_keys_and_the_level_the_cv_states()
    {
        var skills = Parse().Skills.ToDictionary(s => s.Key);

        Assert.Equal(SkillLevel.Basic, skills["excel"].Level);          // "Excel básico"
        Assert.Equal(SkillLevel.Intermediate, skills["word"].Level);    // "Word intermedio"
        Assert.Equal("Atención al cliente", skills["atencion-al-cliente"].Name);
        Assert.True(skills.ContainsKey("facturacion"));
        Assert.True(skills.ContainsKey("gestion-documentaria"));
        Assert.True(skills.ContainsKey("google-drive"));
        Assert.True(skills.ContainsKey("whatsapp-business"));
        Assert.True(skills.ContainsKey("programacion-actividades"));
    }

    [Fact]
    public void Unstated_levels_are_never_overstated()
    {
        var skills = Parse().Skills.ToDictionary(s => s.Key);

        // Only listed under HABILIDADES, never used in a job: basic. Used in a job: intermediate.
        Assert.Equal(SkillLevel.Basic, skills["google-drive"].Level);
        Assert.Equal(SkillLevel.Intermediate, skills["facturacion"].Level);
    }

    [Fact]
    public void Custom_skills_from_the_skills_section_are_kept_with_a_slug_key()
    {
        Assert.Contains(Parse().Skills, s => s.Key == "trabajo-bajo-presion" && s.Name == "Trabajo bajo presión");
    }

    [Fact]
    public void Languages_certifications_name_headline_and_roles_are_extracted()
    {
        var cv = Parse();

        Assert.Contains(cv.Languages, l => l.Name == "Inglés" && l.Level == "Básico");
        Assert.Contains(cv.Languages, l => l.Name == "Español" && l.Level == "Nativo");
        Assert.Contains("Excel básico - Cibertec (2022)", cv.Certifications);
        Assert.Equal("Areli Quispe Mamani", cv.FullNameGuess);
        Assert.StartsWith("Asistente administrativa con experiencia", cv.Headline);
        Assert.Contains("Asistente Administrativo", cv.SuggestedRoles);
        Assert.Contains("Facturación", cv.SuggestedRoles);
    }

    [Theory]
    [InlineData("ene 2022 - dic 2023", 2022, 1, 2023, 12)]
    [InlineData("01/2023 – 11/2023", 2023, 1, 2023, 11)]
    [InlineData("Setiembre 2021 a Marzo 2022", 2021, 9, 2022, 3)]
    [InlineData("2019 - 2021", 2019, 1, 2021, 12)]
    public void Date_ranges_are_understood_in_common_spanish_formats(string line, int y1, int m1, int y2, int m2)
    {
        var span = DateRanges.Find(line);

        Assert.NotNull(span);
        Assert.Equal(new DateOnly(y1, m1, 1), span!.Value.Start);
        Assert.Equal(new DateOnly(y2, m2, 1), span.Value.End);
    }

    [Theory]
    [InlineData("Desde Mar 2024 hasta la fecha")]
    [InlineData("Mar 2024 - Presente")]
    public void Ongoing_ranges_have_no_end(string line)
    {
        var span = DateRanges.Find("Cargo " + line.Replace("Desde ", "").Replace("hasta la fecha", "- Actualidad"));
        Assert.NotNull(span);
        Assert.Null(span!.Value.End);
    }

    [Fact]
    public void A_cv_without_headings_still_yields_skills_and_explains_what_is_missing()
    {
        var cv = ResumeParser.Parse("Soy una persona responsable con manejo de Excel y atención al cliente, busco una oportunidad para crecer profesionalmente en una empresa seria.", Today);

        Assert.Contains(cv.Skills, s => s.Key == "excel");
        Assert.Equal(0, cv.ExperienceMonths);
        Assert.Contains(cv.Warnings, w => w.Contains("experiencia"));
        Assert.Contains(cv.Warnings, w => w.Contains("estudios"));
    }

    [Fact]
    public void Parsing_is_deterministic() =>
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(Parse()),
            System.Text.Json.JsonSerializer.Serialize(Parse()));
}
