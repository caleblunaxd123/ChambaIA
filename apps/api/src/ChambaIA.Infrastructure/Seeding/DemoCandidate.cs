using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Skills;

namespace ChambaIA.Infrastructure.Seeding;

/// <summary>The fictional demo candidate "Areli Demo". Public so tests exercise the same data the app is demoed with.</summary>
public static class DemoCandidate
{
    public static CandidateProfile Profile(Guid userId) => new()
    {
        UserId = userId,
        Headline = "Asistente administrativa con experiencia en atención al usuario y facturación",
        ExperienceMonths = 33,
        EducationLevel = EducationLevel.University,
        EducationStatus = EducationStatus.InProgress,
        Skills =
        [
            Skill("atencion-al-cliente", SkillLevel.Advanced),
            Skill("gestion-documentaria", SkillLevel.Intermediate),
            Skill("facturacion", SkillLevel.Intermediate),
            Skill("google-drive", SkillLevel.Intermediate),
            Skill("word", SkillLevel.Intermediate),
            Skill("excel", SkillLevel.Basic),
            Skill("programacion-actividades", SkillLevel.Intermediate),
            Skill("comunicacion", SkillLevel.Intermediate),
            Skill("whatsapp-business", SkillLevel.Intermediate)
        ],
        Languages = [new LanguageSkill { Name = "Español", Level = "Nativo" }, new LanguageSkill { Name = "Inglés", Level = "Básico" }],
        Experience =
        [
            new WorkExperience
            {
                Title = "Asistente administrativa",
                Company = "Centro Educativo Demo",
                StartDate = new DateOnly(2023, 12, 1),
                Description = "Atención a padres de familia, archivo y gestión documentaria, facturación y coordinación de actividades."
            },
            new WorkExperience
            {
                Title = "Auxiliar de atención al usuario",
                Company = "Clínica Demo",
                StartDate = new DateOnly(2022, 4, 1),
                EndDate = new DateOnly(2023, 11, 1),
                Description = "Atención presencial, telefónica y por WhatsApp; registro de documentos."
            }
        ],
        Education =
        [
            new EducationEntry { Institution = "Instituto Demo", Degree = "Técnico en Administración", Level = EducationLevel.Technical, Status = EducationStatus.Completed },
            new EducationEntry { Institution = "Universidad Demo", Degree = "Administración de Empresas (6.º ciclo)", Level = EducationLevel.University, Status = EducationStatus.InProgress }
        ],
        Certifications = ["Excel básico", "Atención al cliente"],
        OnboardingCompletedAt = DateTimeOffset.UtcNow
    };

    public static JobPreferences Preferences(Guid userId) => new()
    {
        UserId = userId,
        MinSalary = 1800,
        HomeDistrict = "Los Olivos",
        PreferredRoles =
        [
            "Asistente Administrativo", "Auxiliar Administrativo", "Back Office",
            "Asistente Académico", "Facturación", "Operaciones"
        ],
        MaxRequiredEducation = EducationLevel.Technical
    };

    private static ProfileSkill Skill(string key, SkillLevel level) => new()
    {
        Key = key,
        Name = SkillCatalog.FindByKey(key)?.Name ?? key,
        Level = level
    };
}
