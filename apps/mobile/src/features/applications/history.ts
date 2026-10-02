import type { ApplicationEvent, ApplicationStatus } from '@/api/schemas';
import { applicationStatusLabel, formatDateTime } from '@/lib/format';
import type { IconName } from '@/ui/Icon';

export type HistoryLine = { icon: IconName; text: string; when: string };

const arrived: Record<ApplicationStatus, string> = {
  found: 'Quedó como encontrada',
  interested: 'La guardaste como interesante',
  applied: 'Marcaste que postulaste',
  interview: 'Pasó a la etapa de entrevista',
  offer: 'Recibiste una oferta',
  discarded: 'La descartaste',
};

const icons: Record<ApplicationStatus, IconName> = {
  found: 'search',
  interested: 'bookmark',
  applied: 'paper-plane',
  interview: 'calendar',
  offer: 'trophy',
  discarded: 'close-circle',
};

const RANK: Record<ApplicationStatus, number> = { discarded: -1, found: 0, interested: 1, applied: 2, interview: 3, offer: 4 };

/** One history entry in the user's own words. Pure so the wording is tested without rendering anything. */
export function describeEvent(event: ApplicationEvent, now: Date = new Date()): HistoryLine {
  const when = formatDateTime(event.at, now);

  if (event.kind === 'interviewScheduled') {
    return { icon: 'calendar', text: `Agendaste la entrevista para ${formatDateTime(event.interviewDate, now)}`, when };
  }
  if (event.kind === 'interviewCleared') return { icon: 'calendar-clear-outline', text: 'Quitaste la fecha de la entrevista', when };

  const to = event.toStatus;
  if (!to) return { icon: 'ellipse-outline', text: 'Cambió de etapa', when };
  const from = event.fromStatus;
  // Moving forward (or giving up) reads best as what happened; going back or reviving needs both ends to make sense.
  const forward = from === null || to === 'discarded' || (from !== 'discarded' && RANK[to] > RANK[from]);
  const text = forward ? arrived[to] : `Pasó de «${applicationStatusLabel[from]}» a «${applicationStatusLabel[to]}»`;
  return { icon: icons[to], text, when };
}
