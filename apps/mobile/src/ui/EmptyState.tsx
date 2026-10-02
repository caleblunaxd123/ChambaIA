import { View } from 'react-native';

import { Button } from './Button';
import { Icon, type IconName } from './Icon';
import { Text } from './Text';
import { useTheme } from './theme';

type EmptyStateProps = {
  icon?: IconName;
  title: string;
  message?: string;
  actionLabel?: string;
  onAction?: () => void;
  secondaryLabel?: string;
  onSecondary?: () => void;
  tone?: 'brand' | 'danger';
  testID?: string;
};

export function EmptyState({ icon = 'sparkles-outline', title, message, actionLabel, onAction, secondaryLabel, onSecondary, tone = 'brand', testID }: EmptyStateProps) {
  const { colors, spacing } = useTheme();
  const bg = tone === 'danger' ? colors.dangerTint : colors.primaryTint;
  return (
    <View testID={testID} style={{ alignItems: 'center', gap: spacing.md, paddingVertical: spacing.xxl, paddingHorizontal: spacing.xl }}>
      <View style={{ width: 88, height: 88, borderRadius: 44, backgroundColor: bg, alignItems: 'center', justifyContent: 'center', marginBottom: spacing.xs }}>
        <View style={{ width: 60, height: 60, borderRadius: 30, backgroundColor: colors.surface, alignItems: 'center', justifyContent: 'center' }}>
          <Icon name={icon} size={28} tone={tone === 'danger' ? 'danger' : 'primary'} />
        </View>
      </View>
      <Text variant="heading" style={{ textAlign: 'center' }}>{title}</Text>
      {message ? <Text tone="muted" style={{ textAlign: 'center', maxWidth: 320 }}>{message}</Text> : null}
      {actionLabel && onAction ? (
        <Button label={actionLabel} onPress={onAction} variant={tone === 'danger' ? 'secondary' : 'primary'} style={{ alignSelf: 'center', marginTop: spacing.xs }} />
      ) : null}
      {secondaryLabel && onSecondary ? <Button label={secondaryLabel} onPress={onSecondary} variant="ghost" size="sm" style={{ alignSelf: 'center' }} /> : null}
    </View>
  );
}

/** Network/server failure with a retry. Message comes from the API (already Spanish) when available. */
export function ErrorState({ message, onRetry }: { message?: string; onRetry?: () => void }) {
  return (
    <EmptyState
      testID="error-state"
      icon="cloud-offline-outline"
      tone="danger"
      title="No pudimos cargar esto"
      message={message ?? 'Revisa tu conexión e inténtalo de nuevo.'}
      actionLabel={onRetry ? 'Reintentar' : undefined}
      onAction={onRetry}
    />
  );
}

/** Inline error banner for forms (replaces ad-hoc "Card + red text"). */
export function InlineError({ message, testID }: { message: string; testID?: string }) {
  const { colors, radius } = useTheme();
  return (
    <View testID={testID} accessibilityRole="alert" style={{ flexDirection: 'row', gap: 10, alignItems: 'flex-start', padding: 12, borderRadius: radius.md, backgroundColor: colors.dangerTint }}>
      <Icon name="alert-circle" size={20} tone="danger" />
      <Text tone="danger" style={{ flex: 1 }}>{message}</Text>
    </View>
  );
}
