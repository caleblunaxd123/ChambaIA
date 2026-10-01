import type { ReactNode } from 'react';
import { KeyboardAvoidingView, Platform, View } from 'react-native';

import { Icon } from '@/ui/Icon';
import { Screen } from '@/ui/Screen';
import { Text } from '@/ui/Text';
import { useTheme } from '@/ui/theme';

/** Shared frame for login/register: brand header + content. */
export function AuthLayout({ title, subtitle, children }: { title: string; subtitle: string; children: ReactNode }) {
  const { colors, radius } = useTheme();
  return (
    <KeyboardAvoidingView style={{ flex: 1, backgroundColor: colors.bg }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <Screen contentStyle={{ paddingTop: 32, gap: 28 }}>
        <View style={{ gap: 18 }}>
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 10 }}>
            <View style={{ width: 44, height: 44, borderRadius: radius.md, backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center' }}>
              <Icon name="sparkles" size={24} tone="onPrimary" />
            </View>
            <Text variant="title">ChambaIA</Text>
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
