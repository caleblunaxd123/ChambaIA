import type { JobApplication } from '@/api/schemas';

export type TrackerStats = {
  /** Every card the user actually applied to, including ones later discarded (appliedAt is never cleared). */
  applied: number;
  /** Applications that got an interview (or went past it). */
  interviews: number;
  offers: number;
  /** interviews / applied, 0..1; null until there is at least one application. */
  responseRate: number | null;
};

type Card = Pick<JobApplication, 'status' | 'appliedAt' | 'interviewDate'>;

/** The job-search funnel, computed from the tracker cards (no extra API needed). */
export function trackerStats(applications: readonly Card[]): TrackerStats {
  const applied = applications.filter((a) => a.appliedAt !== null || a.status === 'applied' || a.status === 'interview' || a.status === 'offer').length;
  const interviews = applications.filter((a) => a.status === 'interview' || a.status === 'offer' || a.interviewDate !== null).length;
  const offers = applications.filter((a) => a.status === 'offer').length;
  return { applied, interviews, offers, responseRate: applied === 0 ? null : Math.min(1, interviews / applied) };
}
