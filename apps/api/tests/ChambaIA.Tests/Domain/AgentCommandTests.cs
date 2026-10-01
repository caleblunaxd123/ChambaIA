using ChambaIA.Domain.Agent;
using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;

namespace ChambaIA.Tests.Domain;

public class AgentCommandParserTests
{
    private static AgentCommand Single(string text)
    {
        var commands = AgentCommandParser.Parse(text);
        return Assert.Single(commands);
    }

    // The literal examples from the product brief.
    [Fact]
    public void No_me_muestres_trabajos_en_Ate() =>
        Assert.Equal(new AgentCommand(AgentIntent.ExcludeDistrict, Text: "Ate"), Single("No me muestres trabajos en Ate."));

    [Theory]
    [InlineData("mínimo 1800 soles", 1800)]
    [InlineData("Quiero un sueldo mínimo de S/ 2,000", 2000)]
    [InlineData("1800 soles de mínimo", 1800)]
    [InlineData("al menos 2500", 2500)]
    public void Minimum_salary_is_extracted(string text, int amount) =>
        Assert.Equal(new AgentCommand(AgentIntent.SetMinSalary, Number: amount), Single(text));

    [Fact]
    public void Busca_tambien_facturacion_adds_a_role() =>
        Assert.Equal(new AgentCommand(AgentIntent.AddRole, Text: "Facturacion"), Single("busca también facturación"));

    [Fact]
    public void No_me_muestres_call_center_excludes_a_keyword() =>
        Assert.Equal(new AgentCommand(AgentIntent.ExcludeKeyword, Text: "call center"), Single("No me muestres call center."));

    [Theory]
    [InlineData("Máximo una hora de viaje", 60)]
    [InlineData("máximo 45 minutos de viaje", 45)]
    [InlineData("hasta media hora de viaje", 30)]
    [InlineData("no más de 90 minutos de viaje", 90)]
    [InlineData("máximo hora y media de viaje", 90)]
    public void Max_commute_is_extracted(string text, int minutes) =>
        Assert.Equal(new AgentCommand(AgentIntent.SetMaxCommute, Number: minutes), Single(text));

    [Theory]
    [InlineData("Solo trabajos de lunes a viernes", true)]
    [InlineData("no quiero sábados", true)]
    [InlineData("también acepto sábados", false)]
    public void Weekday_preference_is_extracted(string text, bool weekdaysOnly) =>
        Assert.Equal(new AgentCommand(AgentIntent.SetWeekdaysOnly, Flag: weekdaysOnly), Single(text));

    [Theory]
    [InlineData("solo remoto", WorkModality.Remote)]
    [InlineData("quiero trabajo híbrido", WorkModality.Hybrid)]
    [InlineData("prefiero presencial", WorkModality.OnSite)]
    public void Only_modality_is_extracted(string text, WorkModality modality) =>
        Assert.Equal(new AgentCommand(AgentIntent.SetModality, Modality: modality), Single(text));

    [Fact]
    public void Excluding_a_modality_is_extracted() =>
        Assert.Equal(new AgentCommand(AgentIntent.RemoveModality, Modality: WorkModality.Remote), Single("no quiero trabajos remotos"));

    [Fact]
    public void Home_district_is_extracted() =>
        Assert.Equal(new AgentCommand(AgentIntent.SetHomeDistrict, Text: "Los Olivos"), Single("vivo en Los Olivos"));

    [Fact]
    public void Allowing_a_district_again_is_extracted() =>
        Assert.Equal(new AgentCommand(AgentIntent.AllowDistrict, Text: "Ate"), Single("ya puedes mostrarme trabajos en Ate"));

    [Fact]
    public void Removing_a_role_is_extracted() =>
        Assert.Equal(new AgentCommand(AgentIntent.RemoveRole, Text: "Operaciones"), Single("quita operaciones"));

    [Fact]
    public void Aliases_resolve_to_the_canonical_district() =>
        Assert.Equal(new AgentCommand(AgentIntent.ExcludeDistrict, Text: "San Martín de Porres"), Single("no quiero trabajos en SMP"));

    [Fact]
    public void Several_instructions_in_one_message_are_all_extracted()
    {
        var commands = AgentCommandParser.Parse("mínimo 2000 soles, no me muestres trabajos en Ate y máximo una hora de viaje");

        Assert.Contains(new AgentCommand(AgentIntent.SetMinSalary, Number: 2000), commands);
        Assert.Contains(new AgentCommand(AgentIntent.ExcludeDistrict, Text: "Ate"), commands);
        Assert.Contains(new AgentCommand(AgentIntent.SetMaxCommute, Number: 60), commands);
    }

    [Theory]
    [InlineData("hola")]
    [InlineData("¿cómo estás?")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("gracias por todo")]
    public void Chit_chat_is_not_a_command(string text) => Assert.Empty(AgentCommandParser.Parse(text));

    [Fact]
    public void Absurd_salaries_are_ignored() => Assert.Empty(AgentCommandParser.Parse("mínimo 5 soles"));
}

public class AgentCommandApplierTests
{
    private static JobPreferences Prefs() => new() { UserId = Guid.NewGuid(), MinSalary = 1800, PreferredRoles = ["Asistente Administrativo"] };

    [Fact]
    public void Excluding_a_district_removes_it_from_the_preferred_ones()
    {
        var prefs = Prefs();
        prefs.PreferredDistricts = ["Ate", "Comas"];

        var summary = AgentCommandApplier.Apply(prefs, new AgentCommand(AgentIntent.ExcludeDistrict, Text: "Ate"));

        Assert.Equal("Ya no verás trabajos en Ate.", summary);
        Assert.Equal(["Ate"], prefs.ExcludedDistricts);
        Assert.Equal(["Comas"], prefs.PreferredDistricts);
    }

    [Fact]
    public void Commands_are_idempotent()
    {
        var prefs = Prefs();
        var command = new AgentCommand(AgentIntent.ExcludeDistrict, Text: "Ate");

        Assert.NotNull(AgentCommandApplier.Apply(prefs, command));
        Assert.Null(AgentCommandApplier.Apply(prefs, command));
        Assert.Single(prefs.ExcludedDistricts);
    }

    [Fact]
    public void Excluding_one_modality_keeps_the_other_two_when_there_was_no_preference()
    {
        var prefs = Prefs();

        AgentCommandApplier.Apply(prefs, new AgentCommand(AgentIntent.RemoveModality, Modality: WorkModality.Remote));

        Assert.Equal([WorkModality.OnSite, WorkModality.Hybrid], prefs.PreferredModalities);
    }

    [Fact]
    public void Salary_role_and_commute_updates_are_applied()
    {
        var prefs = Prefs();

        Assert.Equal("Sueldo mínimo: S/ 2,200.", AgentCommandApplier.Apply(prefs, new AgentCommand(AgentIntent.SetMinSalary, Number: 2200)));
        Assert.Equal("Viaje máximo: 1 hora.", AgentCommandApplier.Apply(prefs, new AgentCommand(AgentIntent.SetMaxCommute, Number: 60)));
        Assert.NotNull(AgentCommandApplier.Apply(prefs, new AgentCommand(AgentIntent.AddRole, Text: "Facturación")));

        Assert.Equal(2200, prefs.MinSalary);
        Assert.Equal(60, prefs.MaxCommuteMinutes);
        Assert.Contains("Facturación", prefs.PreferredRoles);
    }

    [Fact]
    public void Allow_keyword_removes_it_from_keywords_and_roles()
    {
        var prefs = Prefs();
        prefs.ExcludedKeywords = ["call center"];

        Assert.NotNull(AgentCommandApplier.Apply(prefs, new AgentCommand(AgentIntent.AllowKeyword, Text: "Call Center")));
        Assert.Empty(prefs.ExcludedKeywords);
    }
}
