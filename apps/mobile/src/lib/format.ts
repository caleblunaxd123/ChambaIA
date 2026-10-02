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

export const dimensionLevelLabel = { strong: 'Alto', medium: 'Medio', weak: 'Bajo' } as const;

export const sortLabel = { relevance: 'Relevancia', recent: 'Más recientes', salary: 'Mejor sueldo' } as const;

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

/** "Importadora Pacífico" → "IP", "Areli" → "AR". Used by avatars when there is no logo. */
export function initials(name: string): string {
  const words = name.trim().split(/\s+/).filter((w) => /\p{L}|\d/u.test(w.charAt(0)));
  if (words.length === 0) return '?';
  if (words.length === 1) return words[0].slice(0, 2).toUpperCase();
  return (words[0].charAt(0) + words[1].charAt(0)).toUpperCase();
}

/** Stable index in [0, size) for a string, so the same company always gets the same colour. */
export function pickIndex(text: string, size: number): number {
  let hash = 0;
  for (let i = 0; i < text.length; i++) hash = (hash * 31 + text.charCodeAt(i)) | 0;
  return Math.abs(hash) % size;
}

/** "Buenos días" / "Buenas tardes" / "Buenas noches" by local hour. */
export function greeting(now: Date = new Date()): string {
  const h = now.getHours();
  if (h >= 5 && h < 12) return 'Buenos días';
  if (h >= 12 && h < 19) return 'Buenas tardes';
  return 'Buenas noches';
}

const WEEKDAYS = ['dom', 'lun', 'mar', 'mié', 'jue', 'vie', 'sáb'];
const MONTHS = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'set', 'oct', 'nov', 'dic'];

/** "Hoy", "Mañana" or "vie 3 oct". */
export function formatDayLabel(date: Date, now: Date = new Date()): string {
  const day = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime();
  const diff = Math.round((day(date) - day(now)) / 86_400_000);
  if (diff === 0) return 'Hoy';
  if (diff === 1) return 'Mañana';
  if (diff === -1) return 'Ayer';
  return `${WEEKDAYS[date.getDay()]} ${date.getDate()} ${MONTHS[date.getMonth()]}`;
}

/** "Hoy, 10:30" / "vie 3 oct, 15:00" (local time, 24 h as is usual in Peru). */
export function formatDateTime(iso: string | null, now: Date = new Date()): string {
  if (!iso) return '';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  const time = `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`;
  return `${formatDayLabel(date, now)}, ${time}`;
}

/** The natural next step on the tracker board; null when the card is at the end of the road. */
export const nextApplicationStage: Record<ApplicationStatus, ApplicationStatus | null> = {
  found: 'interested',
  interested: 'applied',
  applied: 'interview',
  interview: 'offer',
  offer: null,
  discarded: null,
};

/** Label of the one-tap button that moves a card to its next stage. */
export const nextStageAction: Partial<Record<ApplicationStatus, string>> = {
  found: 'Me interesa',
  interested: 'Ya postulé',
  applied: 'Me llamaron a entrevista',
  interview: 'Recibí una oferta',
};

export function plural(count: number, one: string, many: string): string {
  return `${count} ${count === 1 ? one : many}`;
}

/** The closest interview that has not happened yet (an hour of grace, so "today at 10:00" still shows at 10:30). */
export function nextInterview<T extends { status: ApplicationStatus; interviewDate: string | null }>(applications: readonly T[], now: Date = new Date()): T | null {
  const from = now.getTime() - 3_600_000;
  return (
    applications
      .filter((a) => a.status === 'interview' && a.interviewDate && new Date(a.interviewDate).getTime() > from)
      .sort((a, b) => new Date(a.interviewDate as string).getTime() - new Date(b.interviewDate as string).getTime())[0] ?? null
  );
}
