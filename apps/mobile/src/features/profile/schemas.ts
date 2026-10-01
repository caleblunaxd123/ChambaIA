import { z } from 'zod';

import type { PreferencesInput, ProfileInput } from '@/api/endpoints';
import type { Preferences, Profile } from '@/api/schemas';

const digits = (message: string, max: number) =>
  z
    .string()
    .trim()
    .regex(/^\d*$/, message)
    .refine((v) => v === '' || Number(v) <= max, `Máximo ${max}.`);

/** Scalar fields of the profile form. Skills are edited as a list outside of the form state. */
export const profileFormSchema = z.object({
  fullName: z.string().trim().min(2, 'Cuéntanos tu nombre.').max(100, 'Es demasiado largo.'),
  headline: z.string().trim().max(200, 'Máximo 200 caracteres.'),
  years: digits('Solo números.', 60),
  months: digits('Solo números.', 11),
});
export type ProfileForm = z.infer<typeof profileFormSchema>;

export function profileToForm(p: Profile): ProfileForm {
  return {
    fullName: p.fullName,
    headline: p.headline ?? '',
    years: String(Math.floor(p.experienceMonths / 12)),
    months: String(p.experienceMonths % 12),
  };
}

export function formToProfileInput(form: ProfileForm, base: Profile, extra: Pick<ProfileInput, 'skills' | 'educationLevel' | 'educationStatus'>): ProfileInput {
  return {
    fullName: form.fullName.trim(),
    headline: form.headline.trim() || null,
    experienceMonths: Number(form.years || 0) * 12 + Number(form.months || 0),
    educationLevel: extra.educationLevel,
    educationStatus: extra.educationLevel ? extra.educationStatus : null,
    skills: extra.skills,
    languages: base.languages,
    experience: base.experience,
    education: base.education,
    certifications: base.certifications,
  };
}

/** Preference fields typed as text in the form; lists and enums are handled directly. */
export const preferencesFormSchema = z.object({
  minSalary: digits('Escribe solo números, por ejemplo 1800.', 100_000),
});
export type PreferencesForm = z.infer<typeof preferencesFormSchema>;

export function preferencesToInput(p: Preferences, overrides: Partial<PreferencesInput>, form: PreferencesForm): PreferencesInput {
  const { updatedAt: _ignored, ...base } = p;
  return { ...base, ...overrides, minSalary: form.minSalary === '' ? null : Number(form.minSalary) };
}
