import type { Preferences, Profile } from '@/api/schemas';

export type StrengthStep = {
  key: 'cv' | 'roles' | 'skills' | 'experience' | 'education' | 'salary' | 'district';
  label: string;
  done: boolean;
  /** Where to fix it. */
  route: '/edit-profile' | '/edit-preferences' | '/onboarding';
};

/**
 * How much the agent knows to match well. Each step is something the matcher actually uses, so the meter is honest:
 * a missing salary or district really does make the results worse.
 */
export function profileStrength(profile: Profile | undefined, prefs: Preferences | undefined, hasCv: boolean) {
  const steps: StrengthStep[] = [
    { key: 'cv', label: 'Sube tu CV', done: hasCv, route: '/onboarding' },
    { key: 'roles', label: 'Dile qué cargos buscas', done: (prefs?.preferredRoles.length ?? 0) > 0, route: '/edit-preferences' },
    { key: 'skills', label: 'Agrega al menos 3 habilidades', done: (profile?.skills.length ?? 0) >= 3, route: '/edit-profile' },
    { key: 'experience', label: 'Indica tu experiencia', done: (profile?.experienceMonths ?? 0) > 0, route: '/edit-profile' },
    { key: 'education', label: 'Indica tus estudios', done: profile?.educationLevel != null, route: '/edit-profile' },
    { key: 'salary', label: 'Define tu sueldo mínimo', done: prefs?.minSalary != null, route: '/edit-preferences' },
    { key: 'district', label: 'Dinos en qué distrito vives', done: prefs?.homeDistrict != null, route: '/edit-preferences' },
  ];
  const done = steps.filter((s) => s.done).length;
  const value = done / steps.length;
  return {
    steps,
    value,
    percent: Math.round(value * 100),
    next: steps.find((s) => !s.done) ?? null,
    label: value >= 1 ? 'Perfil completo' : value >= 0.7 ? 'Casi listo' : value >= 0.4 ? 'Vas bien' : 'Recién empiezas',
  };
}
