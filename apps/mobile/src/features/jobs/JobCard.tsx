import { View } from 'react-native';

import type { FeedItem } from '@/api/schemas';
import { formatRelativeTime, formatSalary, isFresh, modalityLabel } from '@/lib/format';
import { Badge } from '@/ui/Badge';
import { Button } from '@/ui/Button';
import { Card } from '@/ui/Card';
import { Icon } from '@/ui/Icon';
import { Text } from '@/ui/Text';
import { useTheme } from '@/ui/theme';

import { MatchBadge } from './MatchBadge';

type JobCardProps = {
  item: FeedItem;
  onOpen: () => void;
  onInterested?: () => void;
  onDismiss?: () => void;
  busy?: boolean;
};

export function JobCard({ item, onOpen, onInterested, onDismiss, busy }: JobCardProps) {
  const { colors } = useTheme();
  const { job, match } = item;
  const salary = formatSalary(job);
  const saved = match?.status === 'interested';
  const fresh = isFresh(job.postedAt);

  return (
    <Card onPress={onOpen} testID={`job-card-${job.id}`}>
      <View style={{ gap: 12 }}>
        <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 8 }}>
          {match ? <MatchBadge category={match.category} /> : <Badge label="SIN ANALIZAR" tone="neutral" />}
          {fresh ? <Badge label="Nueva" tone="accent" icon="flash" size="sm" /> : null}
        </View>

        <View style={{ gap: 2 }}>
          <Text variant="heading" numberOfLines={2}>{job.title}</Text>
          <Text tone="muted" numberOfLines={1}>{job.company}</Text>
        </View>

        <View style={{ flexDirection: 'row', flexWrap: 'wrap', columnGap: 14, rowGap: 6 }}>
          <Meta icon="location-outline" text={job.district ?? job.city} />
          <Meta icon="business-outline" text={modalityLabel[job.modality]} />
          {salary ? <Meta icon="cash-outline" text={salary} strong /> : <Meta icon="cash-outline" text="Sueldo no indicado" />}
          {job.postedAt ? <Meta icon="time-outline" text={formatRelativeTime(job.postedAt)} /> : null}
        </View>

        {match && (match.topReasons.length > 0 || match.topWarnings.length > 0) ? (
          <View style={{ gap: 5, paddingTop: 10, borderTopWidth: 1, borderTopColor: colors.border }}>
            {match.topReasons.slice(0, 3).map((reason) => (
              <Line key={reason} kind="ok" text={reason} />
            ))}
            {match.topWarnings.slice(0, 2).map((warning) => (
              <Line key={warning} kind="warn" text={warning} />
            ))}
          </View>
        ) : null}

        {onInterested || onDismiss ? (
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
            <Button label="Ver análisis" onPress={onOpen} variant="secondary" size="sm" icon="analytics-outline" />
            {onInterested ? (
              <Button label={saved ? 'Guardada' : 'Me interesa'} onPress={onInterested} variant={saved ? 'ghost' : 'primary'} size="sm" icon={saved ? 'bookmark' : 'bookmark-outline'} disabled={busy || saved} />
            ) : null}
            {onDismiss ? <Button label="Descartar" onPress={onDismiss} variant="ghost" size="sm" disabled={busy} /> : null}
          </View>
        ) : null}
      </View>
    </Card>
  );
}

function Meta({ icon, text, strong }: { icon: 'location-outline' | 'business-outline' | 'cash-outline' | 'time-outline'; text: string; strong?: boolean }) {
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 5 }}>
      <Icon name={icon} size={15} tone="subtle" />
      <Text variant="caption" tone={strong ? 'default' : 'muted'} style={strong ? { fontFamily: 'PlusJakartaSans_700Bold' } : undefined}>
        {text}
      </Text>
    </View>
  );
}

function Line({ kind, text }: { kind: 'ok' | 'warn'; text: string }) {
  return (
    <View style={{ flexDirection: 'row', alignItems: 'flex-start', gap: 8 }}>
      <Icon name={kind === 'ok' ? 'checkmark-circle' : 'alert-circle'} size={16} tone={kind === 'ok' ? 'success' : 'warning'} />
      <Text variant="caption" tone="muted" style={{ flex: 1 }}>{text}</Text>
    </View>
  );
}
