using ChambaIA.Domain.Enums;

namespace ChambaIA.Infrastructure.Seeding;

/// <summary>Fictional offer used to validate the UX before real sources exist. No real companies or people.</summary>
internal sealed record DemoJob(
    string Id,
    string Title,
    string Company,
    string District,
    WorkModality Modality,
    decimal? SalaryMin,
    decimal? SalaryMax,
    int ExperienceMonths,
    EducationLevel? Education,
    bool EducationCompleted,
    (string Skill, SkillLevel? Level)[] Required,
    string[] Preferred,
    string Industry,
    string Schedule,
    bool? WeekdaysOnly,
    double HoursAgo,
    string Description,
    EmploymentType Type = EmploymentType.FullTime);

internal static class DemoJobs
{
    private const SkillLevel Basic = SkillLevel.Basic;
    private const SkillLevel Mid = SkillLevel.Intermediate;
    private const SkillLevel High = SkillLevel.Advanced;

    private const WorkModality OnSite = WorkModality.OnSite;
    private const WorkModality Hybrid = WorkModality.Hybrid;
    private const WorkModality Remote = WorkModality.Remote;

    public static IReadOnlyList<DemoJob> All { get; } =
    [
        new("demo-001", "Asistente Administrativa", "Clínica Santa Aurora", "San Miguel", OnSite, 1900, 2200, 24,
            EducationLevel.Technical, false,
            [("atencion-al-cliente", Mid), ("gestion-documentaria", Basic), ("excel", Mid), ("word", Basic)],
            ["google-drive"], "Salud", "Lunes a viernes, 8:00 a 17:00", true, 0.4,
            "Buscamos asistente administrativa para el área de admisión y archivo de historias clínicas. Atenderás a pacientes en ventanilla, llevarás el control de documentos y apoyarás en reportes semanales en Excel."),

        new("demo-002", "Auxiliar Administrativo", "Distribuidora Andina Norte", "Los Olivos", OnSite, 1800, 2000, 12,
            null, false,
            [("gestion-documentaria", Basic), ("excel", Basic), ("word", Basic)],
            ["facturacion"], "Distribución", "Lunes a viernes, 8:30 a 17:30", true, 1.2,
            "Apoyo en el área administrativa: recepción y archivo de guías de remisión, digitación de pedidos y coordinación con almacén. Se valora conocer emisión de comprobantes."),

        new("demo-003", "Asistente de Facturación", "Importadora Pacífico", "Comas", OnSite, 2100, 2400, 24,
            EducationLevel.Technical, true,
            [("facturacion", Mid), ("excel", Basic), ("atencion-al-cliente", Basic)],
            ["cobranzas"], "Comercio", "Lunes a sábado, 9:00 a 18:00", false, 2.5,
            "Emisión de facturas y boletas electrónicas, conciliación de pagos y atención de consultas de clientes. Requiere experiencia previa en facturación electrónica."),

        new("demo-004", "Back Office Administrativo", "Seguros Andes Protección", "San Isidro", Hybrid, 2600, 3000, 24,
            EducationLevel.Technical, true,
            [("excel", Mid), ("atencion-al-cliente", Mid), ("gestion-documentaria", Mid), ("outlook", Basic)],
            ["sap"], "Seguros", "Lunes a viernes, híbrido 3 días presencial", true, 3.1,
            "Procesamiento de pólizas y endosos, seguimiento de documentación con clientes y reportes en Excel. Esquema híbrido tras el periodo de prueba."),

        new("demo-005", "Asistente Académico", "Instituto Tecnológico Horizonte", "Los Olivos", OnSite, 1700, 2000, 12,
            EducationLevel.Technical, false,
            [("atencion-al-cliente", Basic), ("gestion-documentaria", Basic), ("programacion-actividades", Basic), ("word", Basic)],
            ["google-drive"], "Educación", "Lunes a viernes, 13:00 a 21:00", true, 4.0,
            "Atención a estudiantes, elaboración de horarios, control de asistencia y registro de notas en el sistema académico. Turno tarde-noche."),

        new("demo-006", "Recepcionista Administrativa", "Centro Médico Vida Plena", "Lince", OnSite, 1800, 2000, 12,
            null, false,
            [("atencion-al-cliente", Mid), ("whatsapp-business", Basic), ("programacion-actividades", Basic)],
            ["caja"], "Salud", "Lunes a sábado, turnos rotativos", false, 5.0,
            "Recepción de pacientes, programación de citas por teléfono y WhatsApp, y cobros en caja. Turnos rotativos incluyendo sábados."),

        new("demo-007", "Asistente de Operaciones", "Logística Express", "Ate", OnSite, 2000, 2300, 24,
            null, false,
            [("excel", Mid), ("inventarios", Basic), ("seguimiento-de-pedidos", Basic)],
            ["sap"], "Logística", "Lunes a viernes, 8:00 a 17:00", true, 6.5,
            "Seguimiento de despachos, control de inventarios y reportes diarios para el jefe de operaciones. Centro de distribución en Ate."),

        new("demo-008", "Analista de Facturación y Cobranzas", "Constructora Rímac Sur", "Cercado de Lima", OnSite, 2500, 2900, 36,
            EducationLevel.Technical, true,
            [("facturacion", Mid), ("cobranzas", Mid), ("excel", High)],
            ["sap", "contabilidad-basica"], "Construcción", "Lunes a viernes, 8:00 a 18:00", true, 7.0,
            "Gestión del ciclo de facturación y cobranza a clientes corporativos, conciliaciones y reportes de antigüedad de cartera."),

        new("demo-009", "Ejecutivo de Call Center (Atención Telefónica)", "Contacto Directo SAC", "Cercado de Lima", OnSite, 1200, 1500, 0,
            null, false,
            [("atencion-al-cliente", Basic), ("call-center", Basic)],
            [], "Servicios", "Turnos rotativos, incluye domingos", false, 8.0,
            "Atención de llamadas entrantes en call center. Remuneración básica más comisiones. Turnos rotativos de lunes a domingo."),

        new("demo-010", "Teleoperador Outbound", "Contacto Directo SAC", "Los Olivos", OnSite, 1200, 1800, 0,
            null, false,
            [("call-center", Basic), ("ventas", Basic)],
            [], "Servicios", "Lunes a sábado, 10:00 a 19:00", false, 9.0,
            "Venta de servicios por teléfono desde nuestro call center de Los Olivos. Ingreso variable según metas."),

        new("demo-011", "Jefe de Administración", "Grupo Valdivia", "Miraflores", OnSite, 4500, 5500, 60,
            EducationLevel.University, true,
            [("excel", High), ("recursos-humanos", Mid), ("compras", Mid), ("pagos-y-tesoreria", Mid)],
            ["sap", "ingles"], "Servicios profesionales", "Lunes a viernes, 8:00 a 18:00", true, 10.0,
            "Liderar el área administrativa: presupuesto, compras, planillas y relación con proveedores. Se requiere título universitario y 5 años de experiencia en jefaturas."),

        new("demo-012", "Contador Público Colegiado", "Estudio Contable Valdivia", "Miraflores", OnSite, 3800, 4500, 48,
            EducationLevel.University, true,
            [("contabilidad-basica", High), ("excel", High), ("facturacion", High)],
            ["sap", "power-bi"], "Servicios profesionales", "Lunes a viernes, 9:00 a 18:00", true, 11.0,
            "Cierre contable mensual, declaraciones tributarias y estados financieros. Colegiatura vigente obligatoria."),

        new("demo-013", "Asistente Administrativo Contable", "Estudio Contable Valdivia", "Lince", OnSite, 2000, 2400, 24,
            EducationLevel.Technical, false,
            [("contabilidad-basica", Basic), ("excel", Mid), ("facturacion", Mid), ("gestion-documentaria", Basic)],
            ["sap"], "Servicios profesionales", "Lunes a viernes, 9:00 a 18:00", true, 12.0,
            "Apoyo contable y administrativo: registro de comprobantes, archivo de documentación de clientes y emisión de facturas."),

        new("demo-014", "Asistente de Gerencia", "Textiles Lima Norte", "Los Olivos", OnSite, 2800, 3200, 36,
            EducationLevel.University, false,
            [("programacion-actividades", Mid), ("redaccion", Mid), ("ingles", Mid), ("excel", Mid)],
            ["powerpoint"], "Manufactura", "Lunes a viernes, 8:30 a 18:00", true, 14.0,
            "Gestión de agenda de gerencia, redacción de correspondencia, coordinación de reuniones y apoyo en presentaciones. Inglés intermedio para comunicación con proveedores."),

        new("demo-015", "Auxiliar de Mesa de Partes", "Cooperativa de Ahorro Nuevo Horizonte", "Cercado de Lima", OnSite, 1800, 2100, 6,
            null, false,
            [("gestion-documentaria", Basic), ("atencion-al-cliente", Basic), ("word", Basic), ("digitacion", Basic)],
            ["google-drive"], "Finanzas", "Lunes a viernes, 8:00 a 17:00", true, 16.0,
            "Recepción, registro y derivación de documentos; atención a socios en ventanilla y archivo digital."),

        new("demo-016", "Asistente de Atención al Cliente", "Farmacias Salud Vital", "Comas", OnSite, 1600, 1850, 6,
            null, false,
            [("atencion-al-cliente", Basic), ("caja", Basic)],
            ["whatsapp-business"], "Retail", "Turnos rotativos, incluye fines de semana", false, 20.0,
            "Atención en tienda, manejo de caja y orientación al cliente sobre productos. Turnos rotativos."),

        new("demo-017", "Asistente Administrativo Remoto", "Soluciones Digitales Pacífico", "Miraflores", Remote, 2000, 2400, 24,
            null, false,
            [("google-drive", Mid), ("excel", Mid), ("comunicacion", Basic), ("redaccion", Basic)],
            ["whatsapp-business"], "Tecnología", "Lunes a viernes, 9:00 a 18:00 (remoto)", true, 22.0,
            "Soporte administrativo 100% remoto para equipo comercial: seguimiento de contratos, agenda y reportes. Se trabaja en Google Workspace."),

        new("demo-018", "Asistente Administrativo", "Colegio Los Pinos", "Los Olivos", OnSite, 1800, 1900, 12,
            EducationLevel.Technical, false,
            [("atencion-al-cliente", Basic), ("gestion-documentaria", Mid), ("word", Basic), ("programacion-actividades", Basic)],
            ["facturacion"], "Educación", "Lunes a viernes, 7:30 a 16:30", true, 26.0,
            "Atención a padres de familia, gestión de matrículas, archivo y coordinación de actividades institucionales."),

        new("demo-019", "Auxiliar Administrativo", "Clínica Dental Sonrisa Perfecta", "San Miguel", OnSite, 1800, 2000, 12,
            null, false,
            [("atencion-al-cliente", Basic), ("gestion-documentaria", Basic), ("caja", Basic)],
            ["whatsapp-business"], "Salud", "Lunes a sábado, 9:00 a 18:00", false, 30.0,
            "Recepción de pacientes, control de historias, cobros y coordinación de citas. Incluye sábados."),

        new("demo-020", "Asistente de Compras", "Importadora Pacífico", "Ate", OnSite, 2200, 2500, 24,
            null, false,
            [("compras", Mid), ("excel", Mid), ("logistica", Basic)],
            ["sap"], "Comercio", "Lunes a viernes, 8:30 a 17:30", true, 34.0,
            "Cotización con proveedores, emisión de órdenes de compra y seguimiento de entregas."),

        new("demo-021", "Practicante Administrativo", "Financiera Nuevo Sol", "Miraflores", OnSite, 1100, 1100, 0,
            EducationLevel.University, false,
            [("word", Basic), ("excel", Basic)],
            [], "Finanzas", "Lunes a viernes, 9:00 a 15:00", true, 40.0,
            "Prácticas pre-profesionales en el área administrativa. Subvención mensual.", EmploymentType.Internship),

        new("demo-022", "Cajera", "Supermercados Mi Barrio", "Los Olivos", OnSite, 1500, 1750, 6,
            null, false,
            [("caja", Basic), ("atencion-al-cliente", Basic)],
            [], "Retail", "Turnos rotativos, incluye domingos", false, 44.0,
            "Cobro en caja, arqueo y atención de clientes. Turnos rotativos de lunes a domingo."),

        new("demo-023", "Asistente de Atención al Usuario", "Clínica Santa Aurora", "San Isidro", OnSite, 2000, 2300, 18,
            EducationLevel.Technical, false,
            [("atencion-al-cliente", Mid), ("manejo-de-reclamos", Basic), ("gestion-documentaria", Basic)],
            ["whatsapp-business"], "Salud", "Lunes a viernes, 8:00 a 17:00", true, 48.0,
            "Atención a pacientes y familiares, registro de reclamos y seguimiento hasta su cierre. Coordinación con áreas médicas."),

        new("demo-024", "Asistente de Back Office Operaciones", "Courier Rápido Lima", "Cercado de Lima", OnSite, 1900, 2200, 12,
            null, false,
            [("digitacion", Basic), ("excel", Basic), ("seguimiento-de-pedidos", Basic)],
            ["atencion-al-cliente"], "Logística", "Lunes a viernes, 9:00 a 18:00", true, 52.0,
            "Registro de envíos, seguimiento de pedidos y atención de consultas de clientes por correo y teléfono."),

        new("demo-025", "Coordinadora Administrativa", "Colegio Santa Teresita", "Comas", OnSite, 2300, 2700, 36,
            EducationLevel.Technical, true,
            [("programacion-actividades", Mid), ("atencion-al-cliente", Mid), ("gestion-documentaria", Mid), ("word", Mid), ("excel", Mid)],
            ["google-drive"], "Educación", "Lunes a viernes, 7:30 a 16:30", true, 60.0,
            "Coordinación del área administrativa: cronogramas, personal de apoyo, trámites y documentos institucionales."),

        new("demo-026", "Asistente de Recursos Humanos", "Textiles Lima Norte", "Ate", OnSite, 2200, 2600, 24,
            EducationLevel.University, false,
            [("recursos-humanos", Mid), ("excel", Mid), ("redaccion", Basic)],
            ["sap"], "Manufactura", "Lunes a viernes, 8:00 a 17:00", true, 72.0,
            "Apoyo en reclutamiento, control de asistencia, planillas y archivo de legajos del personal."),

        new("demo-027", "Secretaria Ejecutiva Bilingüe", "Grupo Valdivia", "San Isidro", OnSite, 3200, 3600, 48,
            EducationLevel.Technical, true,
            [("ingles", High), ("programacion-actividades", High), ("redaccion", High)],
            ["powerpoint"], "Servicios profesionales", "Lunes a viernes, 8:30 a 18:00", true, 80.0,
            "Asistencia directa a gerencia general: agenda, viajes, redacción en español e inglés y atención a visitas ejecutivas."),

        new("demo-028", "Facturador(a)", "Distribuidora Andina Norte", "Los Olivos", OnSite, 2000, 2200, 12,
            null, false,
            [("facturacion", Basic), ("excel", Basic)],
            ["sap"], "Distribución", "Lunes a viernes, 8:30 a 17:30", true, 90.0,
            "Emisión de comprobantes electrónicos, registro de guías y conciliación diaria con almacén.")
    ];
}
