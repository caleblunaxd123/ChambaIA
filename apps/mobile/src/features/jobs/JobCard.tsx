import { Pressable, View } from 'react-native';

import type { FeedItem } from '@/api/schemas';
import { formatRelativeTime, formatSalary, isFresh, modalityLabel } from '@/lib/format';
import { Avatar } from '@/ui/Avatar';
import { Badge } from '@/ui/Badge';
import { Button } from '@/ui/Button';
import { Icon } from '@/ui/Icon';
import { Text } from '@/ui/Text';
import { fontFamily, useTheme } from '@/ui/theme';

import { MatchBadge } from './MatchBadge';

type JobCardProps = {
  item: FeedItem;
  onOpen: () => void;
  onSave?: () => void;
  onUnsave?: () => void;
  onDismiss?: () => void;
  busy?: boolean;
};

/**
 * One offer in a list. The body opens the detail; the footer holds the quick reactions. They are siblings, never
 * nested, so web renders valid HTML and screen readers announce each control once.
 */
export function JobCard({ item, onOpen, onSave, onUnsave, onDismiss, busy }: JobCardProps) {
  const { colors, radius, shadow } = useTheme();
  const { job, match } = item;
  const salary = formatSalary(job);
  const saved = match?.status === 'interested';
  const applied = match?.status === 'applied' || match?.status === 'interview' || match?.status === 'offer';
  const fresh = isFresh(job.postedAt) && match?.status === 'new';
  const place = job.modality === 'remote' ? 'Remoto' : `${job.district ?? job.city} · ${modalityLabel[job.modality]}`;

  return (
    <View testID={`job-card-${job.id}`} style={{ backgroundColor: colors.surface, borderRadius: radius.lg, borderWidth: 1, borderColor: colors.border, overflow: 'hidden', ...shadow.card }}>
      <Pressable
        onPress={onOpen}
        accessibilityRole="button"
        accessibilityLabel={`${job.title} en ${job.company}. ${match ? `Compatibilidad: ${match.categoryLabel}.` : ''} Ver detalle`}
        style={({ pressed }) => ({ padding: 16, gap: 12, backgroundColor: pressed ? colors.surfaceMuted : 'transparent' })}
      >
        <View style={{ flexDirection: 'row', gap: 12, alignItems: 'flex-start' }}>
          <Avatar name={job.company} size={46} />
          <View style={{ flex: 1, gap: 2 }}>
            <Text variant="heading" numberOfLines={2}>{job.title}</Text>
            <Text variant="caption" tone="muted" numberOfLines={1}>{job.company}</Text>
          </View>
          {fresh ? <View accessibilityLabel="Nueva" style={{ width: 10, height: 10, borderRadius: 5, backgroundColor: colors.accent, marginTop: 6 }} /> : null}
        </View>

        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 6 }}>
          {match ? <MatchBadge category={match.category} size="sm" /> : <Badge label="Sin analizar" tone="neutral" size="sm" />}
          {applied ? <Badge label="Ya postulaste" tone="info" icon="paper-plane" size="sm" /> : null}
          {fresh ? <Badge label="Nueva" tone="accent" icon="flash" size="sm" /> : null}
        </View>

        <View style={{ gap: 6 }}>
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
            <Icon name="cash-outline" size={16} tone={salary ? 'primary' : 'subtle'} />
            <Text variant="bodyStrong" tone={salary ? 'default' : 'subtle'} style={salary ? { fontFamily: fontFamily.bold } : undefined}>
              {salary ?? 'Sueldo no indicado'}
            </Text>
          </View>
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
            <Icon name="location-outline" size={16} tone="subtle" />
            <Text variant="caption" tone="muted" numberOfLines={1} style={{ flexShrink: 1 }}>{place}</Text>
            {job.postedAt ? (
              <>
                <Text variant="caption" tone="subtle">·</Text>
                <Text variant="caption" tone="subtle">{formatRelativeTime(job.postedAt)}</Text>
              </>
            ) : null}
          </View>
        </View>

        {match && (match.topReasons.length > 0 || match.topWarnings.length > 0) ? (
          <View style={{ gap: 6, padding: 12, borderRadius: radius.md, backgroundColor: colors.bg }}>
            {match.topReasons.slice(0, 2).map((reason) => (
              <Line key={reason} kind="ok" text={reason} />
            ))}
            {match.topWarnings.slice(0, 1).map((warning) => (
              <Line key={warning} kind="warn" text={warning} />
            ))}
            <View style={{ flexDirection: 'row', alignItems: 'center', gap: 4, marginTop: 2 }}>
              <Text variant="caption" tone="primary" style={{ fontFamily: fontFamily.bold }}>Ver por qué encaja</Text>
              <Icon name="arrow-forward" size={14} tone="primary" />
            </View>
          </View>
        ) : null}
      </Pressable>

      {onSave || onDismiss ? (
        <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 8, paddingHorizontal: 10, paddingVertical: 8, borderTopWidth: 1, borderTopColor: colors.border }}>
          {onDismiss && !applied ? (
            <Button label="Descartar" icon="close" variant="ghost" size="sm" onPress={onDismiss} disabled={busy} testID={`job-dismiss-${job.id}`} accessibilityLabel={`Descartar ${job.title}`} />
          ) : (
            <View />
          )}
          {onSave && !applied ? (
            <Button
              label={saved ? 'Guardada' : 'Guardar'}
              icon={saved ? 'bookmark' : 'bookmark-outline'}
              variant={saved ? 'primary' : 'tonal'}
              size="sm"
              onPress={saved ? onUnsave : onSave}
              disabled={busy || (saved && !onUnsave)}
              testID={`job-save-${job.id}`}
              accessibilityLabel={saved ? `Quitar ${job.title} de guardadas` : `Guardar ${job.title}`}
            />
          ) : null}
        </View>
      ) : null}
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
