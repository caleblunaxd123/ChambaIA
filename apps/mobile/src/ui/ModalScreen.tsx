import { useRouter } from 'expo-router';
import { type ReactNode, useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { IconButton } from './Button';
import { ConfirmDialog } from './ConfirmDialog';
import { SCREEN_MAX_WIDTH } from './Screen';
import { Text } from './Text';
import { useTheme } from './theme';

type ModalScreenProps = {
  title: string;
  subtitle?: string;
  children: ReactNode;
  footer: ReactNode;
  /** When true, closing asks before throwing the edits away. */
  dirty?: boolean;
};

/** Frame for full-screen forms opened as modals: close button, scrolling body, pinned action bar. */
export function ModalScreen({ title, subtitle, children, footer, dirty = false }: ModalScreenProps) {
  const router = useRouter();
  const { colors, spacing } = useTheme();
  const insets = useSafeAreaInsets();
  const [confirmClose, setConfirmClose] = useState(false);
  const leave = () => (router.canGoBack() ? router.back() : router.replace('/profile'));
  const close = () => (dirty ? setConfirmClose(true) : leave());

  return (
    <KeyboardAvoidingView style={{ flex: 1, backgroundColor: colors.bg }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <View style={{ paddingTop: Platform.OS === 'ios' ? 16 : insets.top + 8, paddingHorizontal: spacing.lg, paddingBottom: 10, flexDirection: 'row', alignItems: 'center', gap: 12, width: '100%', maxWidth: SCREEN_MAX_WIDTH, alignSelf: 'center' }}>
        <View style={{ flex: 1, gap: 2 }}>
          <Text variant="title">{title}</Text>
          {subtitle ? <Text variant="caption" tone="muted">{subtitle}</Text> : null}
        </View>
        <IconButton icon="close" label="Cerrar" onPress={close} testID="modal-close" />
      </View>

      <ScrollView keyboardShouldPersistTaps="handled" showsVerticalScrollIndicator={false} contentContainerStyle={{ width: '100%', maxWidth: SCREEN_MAX_WIDTH, alignSelf: 'center', padding: spacing.lg, paddingBottom: spacing.xl, gap: spacing.xl }}>
        {children}
      </ScrollView>

      <View style={{ backgroundColor: colors.surface, borderTopWidth: 1, borderTopColor: colors.border, paddingTop: 12, paddingBottom: insets.bottom + 12 }}>
        <View style={{ width: '100%', maxWidth: SCREEN_MAX_WIDTH, alignSelf: 'center', paddingHorizontal: spacing.lg }}>{footer}</View>
      </View>

      <ConfirmDialog
        visible={confirmClose}
        title="¿Descartar los cambios?"
        message="Tienes cambios sin guardar. Si sales ahora, se perderán."
        confirmLabel="Descartar cambios"
        cancelLabel="Seguir editando"
        destructive
        onCancel={() => setConfirmClose(false)}
        onConfirm={() => {
          setConfirmClose(false);
          leave();
        }}
      />
    </KeyboardAvoidingView>
  );
}
