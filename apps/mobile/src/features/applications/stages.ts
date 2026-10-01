import type { ApplicationStatus } from '@/api/schemas';
import type { BadgeTone } from '@/ui/Badge';
import type { IconName } from '@/ui/Icon';

export const APPLICATION_STAGES: ApplicationStatus[] = ['found', 'interested', 'applied', 'interview', 'offer', 'discarded'];

export const stageStyle: Record<ApplicationStatus, { tone: BadgeTone; icon: IconName }> = {
  found: { tone: 'neutral', icon: 'search' },
  interested: { tone: 'brand', icon: 'bookmark' },
  applied: { tone: 'info', icon: 'paper-plane' },
  interview: { tone: 'accent', icon: 'calendar' },
  offer: { tone: 'success', icon: 'trophy' },
  discarded: { tone: 'danger', icon: 'close-circle' },
};
