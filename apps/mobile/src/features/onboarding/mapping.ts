import type { ProfileInput } from '@/api/endpoints';
import type { DetectedResume, EducationLevel, EducationStatus, Profile, Skill } from '@/api/schemas';

/** Everything the user reviews during onboarding, in the shape of the form (years/months as text). */
export type ProfileDraft = {
  fullName: string;
  headline: string;
  years: string;
  months: string;
  educationLevel: EducationLevel | null;
  educationStatus: EducationStatus | null;
  skills: Skill[];
  languages: Profile['languages'];
  certifications: string[];
  experience: Profile['experience'];
  education: Profile['education'];
  roles: string[];
};

export function draftFromProfile(profile: Profile | undefined, fullName: string, roles: string[] = []): ProfileDraft {
  const months = profile?.experienceMonths ?? 0;
  return {
    fullName: profile?.fullName || fullName,
    headline: profile?.headline ?? '',
    years: String(Math.floor(months / 12)),
    months: String(months % 12),
    educationLevel: profile?.educationLevel ?? null,
    educationStatus: profile?.educationStatus ?? null,
    skills: profile?.skills ?? [],
    languages: profile?.languages ?? [],
    certifications: profile?.certifications ?? [],
    experience: profile?.experience ?? [],
    education: profile?.education ?? [],
    roles,
  };
}

/** Case-insensitive union that keeps the first spelling and the original order. */
export function unionText(...lists: string[][]): string[] {
  const seen = new Set<string>();
  const out: string[] = [];
  for (const item of lists.flat()) {
    const clean = item.trim();
    const key = clean.toLowerCase();
    if (clean && !seen.has(key)) {
      seen.add(key);
      out.push(clean);
    }
  }
  return out;
}

/**
 * Lays the parser's proposal over what the user already has. The proposal only fills in what it actually found,
 * and never replaces the name the user typed: the draft stays editable and nothing is saved until the user confirms.
 */
export function applyDetected(draft: ProfileDraft, detected: DetectedResume): ProfileDraft {
  const pick = <T,>(found: T[], current: T[]) => (found.length > 0 ? found : current);
  const months = detected.experienceMonths > 0 ? detected.experienceMonths : Number(draft.years || 0) * 12 + Number(draft.months || 0);

  return {
    ...draft,
    fullName: draft.fullName.trim() || detected.fullNameGuess || '',
    headline: detected.headline ?? draft.headline,
    years: String(Math.floor(months / 12)),
    months: String(months % 12),
    educationLevel: detected.educationLevel ?? draft.educationLevel,
    educationStatus: detected.educationLevel ? detected.educationStatus : draft.educationStatus,
    skills: pick(detected.skills, draft.skills),
    languages: pick(detected.languages, draft.languages),
    certifications: pick(detected.certifications, draft.certifications),
    experience: pick(detected.experience, draft.experience),
    education: pick(detected.education, draft.education),
    roles: unionText(detected.suggestedRoles, draft.roles),
  };
}

export function draftToMonths(draft: Pick<ProfileDraft, 'years' | 'months'>): number {
  return Number(draft.years || 0) * 12 + Number(draft.months || 0);
}

export function draftToProfileInput(draft: ProfileDraft, completeOnboarding = false): ProfileInput {
  return {
    fullName: draft.fullName.trim(),
    headline: draft.headline.trim() || null,
    experienceMonths: draftToMonths(draft),
    educationLevel: draft.educationLevel,
    educationStatus: draft.educationLevel ? draft.educationStatus : null,
    skills: draft.skills,
    languages: draft.languages,
    experience: draft.experience,
    education: draft.education,
    certifications: draft.certifications,
    completeOnboarding,
  };
}

const MONTHS = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'set', 'oct', 'nov', 'dic'];

/** "dic 2023 – actualidad". Dates arrive as ISO yyyy-mm-dd from the API. */
export function formatPeriod(start: string | null, end: string | null): string {
  const fmt = (iso: string) => {
    const [y, m] = iso.split('-');
    return `${MONTHS[Number(m) - 1] ?? ''} ${y}`.trim();
  };
  if (!start) return '';
  return `${fmt(start)} – ${end ? fmt(end) : 'actualidad'}`;
}
