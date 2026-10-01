import { useRouter } from 'expo-router';
import type { ReactNode } from 'react';
import { KeyboardAvoidingView, Platform, View } from 'react-native';

import { IconButton } from '@/ui/Button';
import { Icon } from '@/ui/Icon';
import { Screen } from '@/ui/Screen';
import { Text } from '@/ui/Text';
import { useTheme } from '@/ui/theme';

/** Logo + wordmark. `onHero` renders it white for the gradient panels. */
export function BrandMark({ onHero = false }: { onHero?: boolean }) {
  const { colors, radius } = useTheme();
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 10 }}>
      <View style={{ width: 42, height: 42, borderRadius: radius.md, backgroundColor: onHero ? colors.onHeroTint : colors.primary, alignItems: 'center', justifyContent: 'center' }}>
        <Icon name="sparkles" size={22} tone={onHero ? 'onHero' : 'onPrimary'} />
      </View>
      <Text variant="title" tone={onHero ? 'onHero' : 'default'}>
        Chamba<Text variant="title" tone={onHero ? 'onHero' : 'primary'} style={onHero ? { opacity: 0.75 } : undefined}>IA</Text>
      </Text>
    </View>
  );
}

/** Shared frame for login/register: back to welcome, brand, title + content. */
export function AuthLayout({ title, subtitle, children }: { title: string; subtitle: string; children: ReactNode }) {
  const router = useRouter();
  const { colors } = useTheme();
  return (
    <KeyboardAvoidingView style={{ flex: 1, backgroundColor: colors.bg }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <Screen contentStyle={{ paddingTop: 16, gap: 28 }}>
        <View style={{ gap: 22 }}>
          <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
            <BrandMark />
            <IconButton icon="close" label="Volver al inicio" variant="ghost" onPress={() => (router.canGoBack() ? router.back() : router.replace('/welcome'))} testID="auth-back" />
          </View>
          <View style={{ gap: 6 }}>
            <Text variant="display">{title}</Text>
            <Text tone="muted">{subtitle}</Text>
          </View>
        </View>
        {children}
      </Screen>
    </KeyboardAvoidingView>
  );
}
