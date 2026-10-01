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
  tone?: 'brand' | 'danger';
  testID?: string;
};

export function EmptyState({ icon = 'sparkles-outline', title, message, actionLabel, onAction, tone = 'brand', testID }: EmptyStateProps) {
  const { colors, spacing } = useTheme();
  const bg = tone === 'danger' ? colors.dangerTint : colors.primaryTint;
  return (
    <View testID={testID} style={{ alignItems: 'center', gap: spacing.md, paddingVertical: spacing.xxl, paddingHorizontal: spacing.xl }}>
      <View style={{ width: 72, height: 72, borderRadius: 36, backgroundColor: bg, alignItems: 'center', justifyContent: 'center' }}>
        <Icon name={icon} size={32} tone={tone === 'danger' ? 'danger' : 'primary'} />
      </View>
      <Text variant="heading" style={{ textAlign: 'center' }}>{title}</Text>
      {message ? <Text tone="muted" style={{ textAlign: 'center', maxWidth: 320 }}>{message}</Text> : null}
      {actionLabel && onAction ? <Button label={actionLabel} onPress={onAction} variant={tone === 'danger' ? 'secondary' : 'primary'} /> : null}
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
