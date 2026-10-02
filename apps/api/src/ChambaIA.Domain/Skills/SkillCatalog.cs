using ChambaIA.Domain.Text;

namespace ChambaIA.Domain.Skills;

public sealed record SkillDefinition(string Key, string Name, string[] Aliases);

/// <summary>
/// Canonical skill taxonomy. Candidate skills and job requirements are compared by <see cref="SkillDefinition.Key"/>,
/// never by free text, so "atención al usuario" and "servicio al cliente" count as the same skill.
/// </summary>
public static class SkillCatalog
{
    public static IReadOnlyList<SkillDefinition> All { get; } =
    [
        new("atencion-al-cliente", "Atención al cliente", ["atencion al usuario", "atencion al publico", "servicio al cliente", "atencion presencial", "atencion telefonica", "customer service", "orientacion al usuario", "atencion al paciente", "atencion de pacientes", "atencion al ciudadano"]),
        new("gestion-documentaria", "Gestión documentaria", ["gestion documental", "gestion de documentos", "archivo", "documentacion", "tramite documentario", "mesa de partes", "archivo de documentos"]),
        new("facturacion", "Facturación", ["facturacion electronica", "emision de comprobantes", "emision de facturas", "comprobantes de pago", "sunat", "boletas y facturas"]),
        new("excel", "Excel", ["microsoft excel", "ms excel", "hojas de calculo", "tablas dinamicas"]),
        new("word", "Word", ["microsoft word", "ms word", "procesador de textos"]),
        new("powerpoint", "PowerPoint", ["power point", "presentaciones"]),
        new("google-drive", "Google Drive", ["google workspace", "google docs", "google sheets", "g suite", "drive"]),
        new("outlook", "Outlook", ["correo electronico", "gestion de correos", "microsoft outlook"]),
        new("programacion-actividades", "Programación de actividades", ["gestion de agenda", "coordinacion de agendas", "agenda", "programacion de reuniones", "coordinacion de actividades"]),
        new("redaccion", "Redacción", ["redaccion de documentos", "redaccion de informes", "redaccion de cartas", "elaboracion de informes"]),
        new("organizacion", "Organización", ["organizacion del tiempo", "orden y organizacion", "planificacion"]),
        new("trabajo-en-equipo", "Trabajo en equipo", ["trabajo colaborativo", "colaboracion"]),
        new("comunicacion", "Comunicación efectiva", ["comunicacion asertiva", "habilidades comunicativas", "comunicacion"]),
        new("whatsapp-business", "WhatsApp Business", ["whatsapp", "atencion por whatsapp", "mensajeria instantanea"]),
        new("digitacion", "Digitación", ["digitacion de datos", "data entry", "ingreso de datos", "registro de datos"]),
        new("caja", "Manejo de caja", ["caja", "cobros", "arqueo de caja", "manejo de efectivo"]),
        new("cobranzas", "Cobranzas", ["cobranza", "gestion de cobranza", "recuperacion de cartera"]),
        new("contabilidad-basica", "Contabilidad básica", ["contabilidad", "registro contable", "conciliaciones bancarias"]),
        new("inventarios", "Control de inventarios", ["inventario", "control de stock", "almacen", "kardex"]),
        new("logistica", "Logística", ["coordinacion logistica", "despachos", "distribucion"]),
        new("compras", "Compras", ["gestion de compras", "ordenes de compra", "cotizaciones"]),
        new("ventas", "Ventas", ["venta", "asesoria comercial", "asesor comercial", "venta consultiva"]),
        new("recursos-humanos", "Recursos humanos", ["rrhh", "reclutamiento", "seleccion de personal", "planillas"]),
        new("sap", "SAP", ["sap b1", "sap business one"]),
        new("sql", "SQL", ["bases de datos", "consultas sql"]),
        new("power-bi", "Power BI", ["powerbi", "reportes en power bi"]),
        new("ingles", "Inglés", ["english", "ingles intermedio", "ingles avanzado"]),
        new("call-center", "Call center", ["telemarketing", "teleoperador", "atencion telefonica masiva"]),
        new("seguimiento-de-pedidos", "Seguimiento de pedidos", ["seguimiento de ordenes", "gestion de pedidos", "tracking de pedidos"]),
        new("pagos-y-tesoreria", "Pagos y tesorería", ["tesoreria", "programacion de pagos", "pagos a proveedores"]),
        new("manejo-de-reclamos", "Manejo de reclamos", ["libro de reclamaciones", "atencion de reclamos", "gestion de reclamos"]),
        new("operaciones", "Operaciones", ["back office", "backoffice", "operaciones administrativas"])
    ];

    private static readonly Dictionary<string, SkillDefinition> ByKey = All.ToDictionary(s => s.Key);

    private static readonly Dictionary<string, SkillDefinition> ByAlias = All
        .SelectMany(s => s.Aliases.Append(s.Name).Append(s.Key.Replace('-', ' ')).Select(a => (Alias: TextNormalizer.Normalize(a), Skill: s)))
        .GroupBy(x => x.Alias)
        .ToDictionary(g => g.Key, g => g.First().Skill);

    public static SkillDefinition? FindByKey(string? key) =>
        key is not null && ByKey.TryGetValue(key, out var skill) ? skill : null;

    /// <summary>Resolves a free-text label ("Atención al usuario") to its canonical skill, or null when unknown.</summary>
    public static SkillDefinition? Resolve(string? text) =>
        ByAlias.TryGetValue(TextNormalizer.Normalize(text), out var skill) ? skill : null;

    /// <summary>Finds every catalogued skill mentioned in a longer text (used by the CV parser in phase 2).</summary>
    public static IReadOnlyList<SkillDefinition> DetectIn(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var normalized = $" {TextNormalizer.Normalize(text)} ";
        return ByAlias
            .Where(kv => kv.Key.Length > 2 && normalized.Contains($" {kv.Key} ", StringComparison.Ordinal))
            .Select(kv => kv.Value)
            .Distinct()
            .ToList();
    }
}
