import type { ApplicationStatus, EducationLevel, JobSummary, MatchCategory, NotificationFrequency, SkillLevel, WorkModality } from '@/api/schemas';

/** Pure formatting helpers (no React) so they are trivially unit-testable. */

export function formatDuration(totalMonths: number): string {
  if (totalMonths <= 0) return 'Sin experiencia';
  const years = Math.floor(totalMonths / 12);
  const months = totalMonths % 12;
  const y = years === 0 ? '' : years === 1 ? '1 año' : `${years} años`;
  const m = months === 0 ? '' : months === 1 ? '1 mes' : `${months} meses`;
  return y && m ? `${y} y ${m}` : y || m;
}

export function formatMoney(amount: number): string {
  return `S/ ${Math.round(amount).toLocaleString('en-US')}`;
}

/** "S/ 1,900 - S/ 2,200", "Desde S/ 1,800" or null when the offer hides its salary. */
export function formatSalary(job: Pick<JobSummary, 'salaryMin' | 'salaryMax'>): string | null {
  const { salaryMin: min, salaryMax: max } = job;
  if (min == null && max == null) return null;
  if (min != null && max != null) return min === max ? formatMoney(min) : `${formatMoney(min)} - ${formatMoney(max)}`;
  return min != null ? `Desde ${formatMoney(min)}` : `Hasta ${formatMoney(max as number)}`;
}

/** "hace 15 min", "hace 3 h", "ayer", "hace 4 días". */
export function formatRelativeTime(iso: string | null, now: Date = new Date()): string {
  if (!iso) return '';
  const diffMs = now.getTime() - new Date(iso).getTime();
  if (Number.isNaN(diffMs)) return '';
  const minutes = Math.floor(diffMs / 60_000);
  if (minutes < 1) return 'ahora mismo';
  if (minutes < 60) return `hace ${minutes} min`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `hace ${hours} h`;
  const days = Math.floor(hours / 24);
  if (days === 1) return 'ayer';
  if (days < 30) return `hace ${days} días`;
  return 'hace más de un mes';
}

export function isFresh(iso: string | null, now: Date = new Date(), hours = 24): boolean {
  if (!iso) return false;
  return now.getTime() - new Date(iso).getTime() < hours * 3_600_000;
}

export const modalityLabel: Record<WorkModality, string> = { onSite: 'Presencial', hybrid: 'Híbrido', remote: 'Remoto' };

export const educationLabel: Record<EducationLevel, string> = {
  secondary: 'Secundaria',
  technical: 'Técnico',
  university: 'Universitario',
  postgraduate: 'Posgrado',
};

export const skillLevelLabel: Record<SkillLevel, string> = { basic: 'Básico', intermediate: 'Intermedio', advanced: 'Avanzado' };

export const frequencyLabel: Record<NotificationFrequency, string> = {
  instant: 'Al instante',
  every2Hours: 'Cada 2 horas',
  every6Hours: 'Cada 6 horas',
  daily: 'Una vez al día',
};

export const applicationStatusLabel: Record<ApplicationStatus, string> = {
  found: 'Encontrada',
  interested: 'Interesado',
  applied: 'Postulado',
  interview: 'Entrevista',
  offer: 'Oferta',
  discarded: 'Descartado',
};

/** Compatibility is shown as a category, never as a percentage that would overstate certainty. */
export const categoryOrder: MatchCategory[] = ['excellent', 'veryCompatible', 'compatible', 'review', 'poor'];

export function firstName(fullName: string | undefined): string {
  return (fullName ?? '').trim().split(/\s+/)[0] ?? '';
}
