import { useRouter } from 'expo-router';
import type { ReactNode } from 'react';
import { KeyboardAvoidingView, Platform, Pressable, ScrollView, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Icon } from './Icon';
import { SCREEN_MAX_WIDTH } from './Screen';
import { Text } from './Text';
import { useTheme } from './theme';

type ModalScreenProps = {
  title: string;
  children: ReactNode;
  footer: ReactNode;
};

/** Frame for full-screen forms opened as modals: close button, scrolling body, pinned action bar. */
export function ModalScreen({ title, children, footer }: ModalScreenProps) {
  const router = useRouter();
  const { colors, spacing } = useTheme();
  const insets = useSafeAreaInsets();
  const close = () => (router.canGoBack() ? router.back() : router.replace('/profile'));

  return (
    <KeyboardAvoidingView style={{ flex: 1, backgroundColor: colors.bg }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <View style={{ paddingTop: Platform.OS === 'ios' ? 16 : insets.top + 8, paddingHorizontal: spacing.lg, paddingBottom: 10, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', width: '100%', maxWidth: SCREEN_MAX_WIDTH, alignSelf: 'center' }}>
        <Text variant="title">{title}</Text>
        <Pressable onPress={close} accessibilityLabel="Cerrar" hitSlop={12} testID="modal-close" style={{ width: 40, height: 40, borderRadius: 20, backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border, alignItems: 'center', justifyContent: 'center' }}>
          <Icon name="close" size={22} />
        </Pressable>
      </View>

      <ScrollView keyboardShouldPersistTaps="handled" showsVerticalScrollIndicator={false} contentContainerStyle={{ width: '100%', maxWidth: SCREEN_MAX_WIDTH, alignSelf: 'center', padding: spacing.lg, paddingBottom: spacing.xl, gap: spacing.xl }}>
        {children}
      </ScrollView>

      <View style={{ backgroundColor: colors.surface, borderTopWidth: 1, borderTopColor: colors.border, paddingTop: 12, paddingBottom: insets.bottom + 12 }}>
        <View style={{ width: '100%', maxWidth: SCREEN_MAX_WIDTH, alignSelf: 'center', paddingHorizontal: spacing.lg }}>{footer}</View>
      </View>
    </KeyboardAvoidingView>
  );
}
