using ChambaIA.Domain.Ai;
using ChambaIA.Domain.Enums;

namespace ChambaIA.Tests.Domain;

/// <summary>
/// The guard against well-formed, plausible, wrong answers. The first cases are the real mistakes measured with local models
/// (llama3.2 and llama3.1:8b) on "Pagamos S/ 1,100 semanales".
/// </summary>
public class JobExtractionGroundingTests
{
    private static JobExtractionResult Answer(decimal? min = null, decimal? max = null, int? months = null, bool? weekdays = null, EducationLevel? education = null) =>
        new(min, max, months, weekdays, education, []);

    private static JobExtractionResult Ground(JobExtractionResult answer, string text) => JobExtractionGrounding.Ground(answer, text);

    // ---- salary ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_number_the_model_made_up_is_dropped_even_though_it_looks_like_a_normal_salary()
    {
        // Measured: "S/ 1,100 semanales" came back as 11000 a month.
        var r = Ground(Answer(min: 11000), "Pagamos S/ 1,100 semanales. Experiencia en almacén deseable.");

        Assert.Null(r.SalaryMin);
    }

    [Theory]
    [InlineData("Pagamos S/ 1,100 semanales.")]
    [InlineData("Remuneración de 1100 por hora trabajada")]
    [InlineData("Sueldo quincenal de 1100 soles")]
    [InlineData("1100 soles al día más bono")]
    [InlineData("Jornal de 1100")]
    public void A_salary_that_is_not_monthly_is_never_taken_as_monthly(string text)
    {
        // Measured: the weekly amount came back as 1100 a month.
        Assert.Null(Ground(Answer(min: 1100), text).SalaryMin);
    }

    [Theory]
    [InlineData("Sueldo: S/ 1,800 mensuales", 1800)]
    [InlineData("Remuneración 1.800 soles", 1800)]
    [InlineData("Pagamos 1800 netos", 1800)]
    [InlineData("Sueldo S/ 1,800.00 más bonos", 1800)]
    [InlineData("Ofrecemos 2500,50 al mes", 2500)]
    public void A_salary_written_in_the_text_in_any_common_format_is_kept(string text, int value)
    {
        Assert.Equal(value, Ground(Answer(min: value), text).SalaryMin);
    }

    [Fact]
    public void Each_end_of_the_range_is_checked_on_its_own_and_a_lone_maximum_stays_a_maximum()
    {
        var both = Ground(Answer(min: 1800, max: 2200), "Remuneración entre 1800 y 2200 soles.");
        Assert.Equal((1800m, 2200m), (both.SalaryMin, both.SalaryMax));

        var oneInvented = Ground(Answer(min: 1800, max: 2500), "Remuneración entre 1800 y 2200 soles.");
        Assert.Equal((1800m, null), (oneInvented.SalaryMin, oneInvented.SalaryMax));

        var onlyMax = Ground(Answer(max: 2000), "Pagamos hasta 2000 soles.");
        Assert.Equal((null, 2000m), (onlyMax.SalaryMin, onlyMax.SalaryMax));
    }

    // ---- experience --------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Mínimo 2 años de experiencia en ventas", 24)]
    [InlineData("Contar con un año de experiencia", 12)]
    [InlineData("Se requieren tres años en puesto similar", 36)]
    [InlineData("Experiencia de seis meses en caja", 6)]
    [InlineData("Mínimo 18 meses de experiencia", 18)]
    [InlineData("Experiencia mínima de un año y medio", 18)]
    [InlineData("Con medio año de experiencia es suficiente", 6)]
    public void Experience_is_kept_only_when_the_text_states_that_duration(string text, int months)
    {
        Assert.Equal(months, Ground(Answer(months: months), text).ExperienceMonths);
    }

    [Fact]
    public void A_duration_the_offer_never_says_is_dropped()
    {
        Assert.Null(Ground(Answer(months: 24), "Contar con 1 año de experiencia en ventas").ExperienceMonths);
        Assert.Null(Ground(Answer(months: 12), "Buscamos personal con experiencia").ExperienceMonths);
    }

    [Theory]
    [InlineData("No se requiere experiencia, nosotros te capacitamos")]
    [InlineData("Sin experiencia previa")]
    [InlineData("No requiere experiencia")]
    public void No_experience_is_zero_only_when_the_text_says_so(string text)
    {
        Assert.Equal(0, Ground(Answer(months: 0), text).ExperienceMonths);
        Assert.Null(Ground(Answer(months: 0), "Se valora la experiencia en tiendas").ExperienceMonths);
    }

    // ---- schedule and education --------------------------------------------------------------------------------------------

    [Fact]
    public void The_schedule_flag_needs_the_words_that_justify_it()
    {
        Assert.True(Ground(Answer(weekdays: true), "Horario de lunes a viernes de 9 a 6").WeekdaysOnly);
        Assert.False(Ground(Answer(weekdays: false), "Turnos rotativos incluyendo fines de semana").WeekdaysOnly);
        Assert.False(Ground(Answer(weekdays: false), "Lunes a sábado de 8 a 5").WeekdaysOnly);
        Assert.Null(Ground(Answer(weekdays: true), "Horario flexible").WeekdaysOnly);
        Assert.Null(Ground(Answer(weekdays: false), "Horario de oficina").WeekdaysOnly);
    }

    [Theory]
    [InlineData(EducationLevel.Secondary, "Secundaria completa")]
    [InlineData(EducationLevel.Technical, "Técnico en administración o carreras afines")]
    [InlineData(EducationLevel.Technical, "Egresado de instituto")]
    [InlineData(EducationLevel.University, "Bachiller o titulado en contabilidad")]
    [InlineData(EducationLevel.Postgraduate, "Maestría en gestión")]
    public void Education_is_kept_when_the_text_mentions_it(EducationLevel level, string text)
    {
        Assert.Equal(level, Ground(Answer(education: level), text).Education);
    }

    [Fact]
    public void Education_the_text_never_mentions_is_dropped()
    {
        Assert.Null(Ground(Answer(education: EducationLevel.University), "Buscamos personal responsable y puntual").Education);
    }

    [Fact]
    public void Grounding_keeps_the_skills_and_never_invents_fields()
    {
        var skills = new[] { new ExtractedSkill("excel", "Excel", SkillLevel.Basic, true) };

        var r = Ground(new JobExtractionResult(null, null, null, null, null, skills), "Texto cualquiera");

        Assert.Equal(skills, r.Skills);
        Assert.Equal((null, null, null, null, null), (r.SalaryMin, r.SalaryMax, r.ExperienceMonths, r.WeekdaysOnly, r.Education));
    }
}
