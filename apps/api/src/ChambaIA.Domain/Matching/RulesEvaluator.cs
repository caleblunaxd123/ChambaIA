using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Geo;
using ChambaIA.Domain.Text;

namespace ChambaIA.Domain.Matching;

/// <summary>
/// Stage A: hard rules ("this offer conflicts with what you asked") plus soft penalties.
/// A hard failure means the offer is hidden from the feed; it is never ranked above "Poco compatible".
/// </summary>
internal static class RulesEvaluator
{
    private const int ExperienceToleranceMonths = 12;

    public static StageResult Evaluate(CandidateProfile profile, JobPreferences prefs, JobOffer job)
    {
        var r = new StageResult();

        CheckDistrict(prefs, job, r);
        CheckSalary(prefs, job, r);
        CheckModality(prefs, job, r);
        CheckEmploymentType(prefs, job, r);
        CheckExcludedTerms(prefs, job, r);
        CheckEducationCeiling(prefs, job, r);
        CheckEducationFit(profile, job, r);
        CheckCommute(prefs, job, r);
        CheckSchedule(prefs, job, r);
        CheckExperience(profile, job, r);

        if (r.HardFailures.Count > 0) r.Score = 0;
        return r;
    }

    public static double ExperienceScore(CandidateProfile profile, JobOffer job)
    {
        var required = job.ExperienceRequiredMinMonths;
        if (required is null or 0) return 80;
        return Math.Clamp(100.0 * profile.ExperienceMonths / required.Value, 0, 100);
    }

    public static double EducationScore(CandidateProfile profile, JobOffer job)
    {
        if (job.EducationRequired is null) return 80;
        if (profile.EducationLevel is null) return 60;
        return MeetsEducation(profile, job) ? 100 : 40;
    }

    private static void CheckDistrict(JobPreferences prefs, JobOffer job, StageResult r)
    {
        if (job.Modality == WorkModality.Remote || string.IsNullOrWhiteSpace(job.District)) return;

        if (prefs.ExcludedDistricts.Any(d => TextNormalizer.SameText(d, job.District)))
        {
            r.HardFailures.Add(new MatchNote("excluded-district", $"Está en {job.District}",
                "Pediste no ver trabajos en este distrito."));
            return;
        }

        if (prefs.PreferredDistricts.Count == 0) return;

        if (prefs.PreferredDistricts.Any(d => TextNormalizer.SameText(d, job.District)))
            r.Reasons.Add(new MatchNote("preferred-district", $"En {job.District}, uno de tus distritos preferidos"));
        else
        {
            r.Score -= 12;
            r.Warnings.Add(new MatchNote("outside-preferred-districts", $"Está en {job.District}",
                "No está entre tus distritos preferidos."));
        }
    }

    private static void CheckSalary(JobPreferences prefs, JobOffer job, StageResult r)
    {
        var top = job.SalaryMax ?? job.SalaryMin;
        if (top is null)
        {
            r.Score -= 8;
            r.Warnings.Add(new MatchNote("salary-unknown", "No indica sueldo", "Confírmalo antes de postular."));
            return;
        }

        if (prefs.MinSalary is { } min && top < min)
        {
            r.HardFailures.Add(new MatchNote("salary-below-min",
                $"Paga hasta {MatchFormatting.Money(top.Value)}",
                $"Tu mínimo es {MatchFormatting.Money(min)}."));
            return;
        }

        var range = MatchFormatting.SalaryRange(job.SalaryMin, job.SalaryMax);
        r.Reasons.Add(prefs.MinSalary is { } m
            ? new MatchNote("salary-ok", $"Sueldo {range}", $"Alcanza tu mínimo de {MatchFormatting.Money(m)}.")
            : new MatchNote("salary-listed", $"Sueldo {range}"));
    }

    private static void CheckModality(JobPreferences prefs, JobOffer job, StageResult r)
    {
        if (prefs.PreferredModalities.Count == 0) return;
        if (!prefs.PreferredModalities.Contains(job.Modality))
            r.HardFailures.Add(new MatchNote("modality-excluded", $"Modalidad {ModalityLabel(job.Modality)}",
                "No coincide con las modalidades que elegiste."));
        else if (job.Modality == WorkModality.Remote)
            r.Reasons.Add(new MatchNote("modality-remote", "Es remoto", "Sin viaje."));
    }

    private static void CheckEmploymentType(JobPreferences prefs, JobOffer job, StageResult r)
    {
        if (prefs.EmploymentTypes.Count == 0 || prefs.EmploymentTypes.Contains(job.EmploymentType)) return;
        r.HardFailures.Add(new MatchNote("employment-type-excluded", "Tipo de contrato distinto al que buscas"));
    }

    private static void CheckExcludedTerms(JobPreferences prefs, JobOffer job, StageResult r)
    {
        var roleHit = prefs.ExcludedRoles.FirstOrDefault(role => TextNormalizer.ContainsPhrase(job.Title, role));
        if (roleHit is not null)
        {
            r.HardFailures.Add(new MatchNote("excluded-role", $"Es un cargo de «{roleHit}»", "Pediste no ver este tipo de cargo."));
            return;
        }

        var keywordHit = prefs.ExcludedKeywords.FirstOrDefault(k =>
            TextNormalizer.ContainsPhrase(job.Title, k) || TextNormalizer.ContainsPhrase(job.Description, k));
        if (keywordHit is not null)
            r.HardFailures.Add(new MatchNote("excluded-keyword", $"Menciona «{keywordHit}»", "Pediste no ver ofertas con este término."));
    }

    private static void CheckEducationCeiling(JobPreferences prefs, JobOffer job, StageResult r)
    {
        if (prefs.MaxRequiredEducation is not { } ceiling) return;
        if (job.EducationRequired is { } required && job.EducationRequiredCompleted && required > ceiling)
            r.HardFailures.Add(new MatchNote("education-above-ceiling",
                $"Exige {MatchFormatting.Education(required, true)}",
                "Pediste no ver trabajos que exijan ese nivel de estudios terminado."));
    }

    private static void CheckEducationFit(CandidateProfile profile, JobOffer job, StageResult r)
    {
        if (job.EducationRequired is not { } required || r.HardFailures.Any(h => h.Code == "education-above-ceiling")) return;
        if (profile.EducationLevel is null) return;

        var needed = MatchFormatting.Education(required, job.EducationRequiredCompleted);
        if (MeetsEducation(profile, job))
            r.Reasons.Add(new MatchNote("education-ok", $"Piden {needed}", "Tu formación lo cubre."));
        else
        {
            r.Score -= 15;
            r.Warnings.Add(new MatchNote("education-gap", $"Piden {needed}",
                $"Tu perfil indica {MatchFormatting.Education(profile.EducationLevel.Value, profile.EducationStatus == EducationStatus.Completed)}."));
        }
    }

    private static void CheckCommute(JobPreferences prefs, JobOffer job, StageResult r)
    {
        if (job.Modality == WorkModality.Remote) return;
        var home = LimaDistricts.Find(prefs.HomeDistrict);
        if (home is null) return;

        var target = LimaDistricts.Find(job.District);
        var lat = job.Latitude ?? target?.Latitude;
        var lon = job.Longitude ?? target?.Longitude;
        if (CommuteEstimator.EstimateMinutes(home, lat, lon) is not { } minutes) return;

        if (prefs.MaxCommuteMinutes is { } max && minutes > max)
        {
            r.HardFailures.Add(new MatchNote("commute-too-long", $"Unos {minutes} min de viaje",
                $"Tu máximo es {max} min desde {home.Name}."));
            return;
        }

        if (minutes <= 45)
            r.Reasons.Add(new MatchNote("commute-short", "Queda cerca de ti", $"Aprox. {minutes} min desde {home.Name}."));
        else if (minutes > 75)
        {
            r.Score -= 10;
            r.Warnings.Add(new MatchNote("commute-long", "Viaje largo", $"Aprox. {minutes} min desde {home.Name}."));
        }
    }

    private static void CheckSchedule(JobPreferences prefs, JobOffer job, StageResult r)
    {
        if (!prefs.WeekdaysOnly) return;
        if (job.WeekdaysOnly == false)
            r.HardFailures.Add(new MatchNote("schedule-weekends", "Incluye fines de semana", "Pediste solo lunes a viernes."));
        else if (job.WeekdaysOnly == true)
            r.Reasons.Add(new MatchNote("schedule-weekdays", "Horario de lunes a viernes"));
    }

    private static void CheckExperience(CandidateProfile profile, JobOffer job, StageResult r)
    {
        var required = job.ExperienceRequiredMinMonths;
        if (required is null) return;

        if (required == 0)
        {
            r.Reasons.Add(new MatchNote("experience-none", "No exigen experiencia previa"));
            return;
        }

        var have = profile.ExperienceMonths;
        var needed = MatchFormatting.Duration(required.Value);
        if (have >= required)
            r.Reasons.Add(new MatchNote("experience-ok", $"Piden {needed} de experiencia", $"Tienes {MatchFormatting.Duration(have)}."));
        else if (required - have <= ExperienceToleranceMonths)
        {
            r.Score -= 15;
            r.Warnings.Add(new MatchNote("experience-close", $"Piden {needed} de experiencia",
                $"Tienes {MatchFormatting.Duration(have)}: te faltan {MatchFormatting.Duration(required.Value - have)}."));
        }
        else
            r.HardFailures.Add(new MatchNote("experience-too-high", $"Exigen {needed} de experiencia",
                $"Tienes {MatchFormatting.Duration(have)}."));
    }

    private static bool MeetsEducation(CandidateProfile profile, JobOffer job)
    {
        if (job.EducationRequired is not { } required || profile.EducationLevel is not { } level) return true;
        if (level > required) return true;
        return level == required && (!job.EducationRequiredCompleted || profile.EducationStatus == EducationStatus.Completed);
    }

    private static string ModalityLabel(WorkModality m) => m switch
    {
        WorkModality.OnSite => "presencial",
        WorkModality.Hybrid => "híbrida",
        _ => "remota"
    };
}
