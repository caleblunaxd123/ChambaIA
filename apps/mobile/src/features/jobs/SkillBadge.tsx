import { Badge } from '@/ui/Badge';

type SkillBadgeProps = {
  name: string;
  /** matched = the candidate has it, missing = required but absent, neutral = just listing. */
  state?: 'matched' | 'missing' | 'neutral';
};

export function SkillBadge({ name, state = 'neutral' }: SkillBadgeProps) {
  if (state === 'matched') return <Badge label={name} tone="success" icon="checkmark" />;
  if (state === 'missing') return <Badge label={name} tone="warning" icon="alert-circle-outline" />;
  return <Badge label={name} tone="neutral" />;
}
