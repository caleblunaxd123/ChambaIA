import { Modal, Pressable, View } from 'react-native';

import { Button } from './Button';
import { Text } from './Text';
import { useTheme } from './theme';

type ConfirmDialogProps = {
  visible: boolean;
  title: string;
  message?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  destructive?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
};

/** Centered modal for yes/no decisions. */
export function ConfirmDialog({ visible, title, message, confirmLabel = 'Confirmar', cancelLabel = 'Cancelar', destructive, onConfirm, onCancel }: ConfirmDialogProps) {
  const { colors, radius, spacing, shadow } = useTheme();
  return (
    <Modal transparent visible={visible} animationType="fade" onRequestClose={onCancel} statusBarTranslucent>
      <Pressable
        onPress={onCancel}
        accessibilityLabel="Cerrar"
        style={{ flex: 1, backgroundColor: colors.overlay, alignItems: 'center', justifyContent: 'center', padding: spacing.xl }}
      >
        <Pressable
          onPress={() => undefined}
          style={{ width: '100%', maxWidth: 380, backgroundColor: colors.surface, borderRadius: radius.xl, padding: spacing.xl, gap: spacing.md, ...shadow.raised }}
        >
          <Text variant="title">{title}</Text>
          {message ? <Text tone="muted">{message}</Text> : null}
          <View style={{ gap: spacing.sm, marginTop: spacing.sm }}>
            <Button label={confirmLabel} onPress={onConfirm} variant={destructive ? 'danger' : 'primary'} fullWidth />
            <Button label={cancelLabel} onPress={onCancel} variant="ghost" fullWidth />
          </View>
        </Pressable>
      </Pressable>
    </Modal>
  );
}
