import type { MatchCategory } from '@/api/schemas';
import { Badge, type BadgeTone } from '@/ui/Badge';
import type { IconName } from '@/ui/Icon';

type CategoryStyle = { tone: BadgeTone; icon: IconName; label: string };

export const categoryStyle: Record<MatchCategory, CategoryStyle> = {
  excellent: { tone: 'success', icon: 'star', label: 'Excelente opción' },
  veryCompatible: { tone: 'brand', icon: 'thumbs-up', label: 'Muy compatible' },
  compatible: { tone: 'info', icon: 'checkmark-circle', label: 'Compatible' },
  review: { tone: 'warning', icon: 'eye', label: 'Revisar' },
  poor: { tone: 'neutral', icon: 'remove-circle', label: 'Poco compatible' },
};

/** Compatibility as a category + icon (never a percentage). Colour is never the only signal: the label is always shown. */
export function MatchBadge({ category, size = 'md' }: { category: MatchCategory; size?: 'md' | 'sm' }) {
  const s = categoryStyle[category];
  return <Badge label={s.label} tone={s.tone} icon={s.icon} size={size} testID={`match-${category}`} />;
}
