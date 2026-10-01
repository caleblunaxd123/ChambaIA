import type { ReactNode } from 'react';
import { Pressable, View } from 'react-native';

import { haptics } from '@/lib/haptics';

import { Icon, type IconName } from './Icon';
import { Text } from './Text';
import { type TextTone, useTheme } from './theme';

type ListRowProps = {
  icon: IconName;
  title: string;
  subtitle?: string;
  onPress?: () => void;
  tone?: Extract<TextTone, 'default' | 'danger' | 'primary'>;
  right?: ReactNode;
  testID?: string;
};

/** Settings-style row: tinted icon, title + subtitle, chevron when it navigates. */
export function ListRow({ icon, title, subtitle, onPress, tone = 'default', right, testID }: ListRowProps) {
  const { colors, radius } = useTheme();
  const iconBg = tone === 'danger' ? colors.dangerTint : colors.primaryTint;
  const iconTone = tone === 'danger' ? 'danger' : 'primary';

  return (
    <Pressable
      testID={testID}
      accessibilityRole={onPress ? 'button' : undefined}
      disabled={!onPress}
      onPress={() => {
        haptics.tap();
        onPress?.();
      }}
      style={({ pressed }) => ({ flexDirection: 'row', alignItems: 'center', gap: 12, paddingVertical: 12, paddingHorizontal: 14, backgroundColor: pressed ? colors.surfaceMuted : 'transparent', borderRadius: radius.md })}
    >
      <View style={{ width: 38, height: 38, borderRadius: 12, backgroundColor: iconBg, alignItems: 'center', justifyContent: 'center' }}>
        <Icon name={icon} size={19} tone={iconTone} />
      </View>
      <View style={{ flex: 1, gap: 1 }}>
        <Text variant="bodyStrong" tone={tone === 'danger' ? 'danger' : 'default'} numberOfLines={1}>{title}</Text>
        {subtitle ? <Text variant="caption" tone="muted" numberOfLines={2}>{subtitle}</Text> : null}
      </View>
      {right ?? (onPress ? <Icon name="chevron-forward" size={18} tone="subtle" /> : null)}
    </Pressable>
  );
}

/** Groups rows inside one card with hairline separators. */
export function ListGroup({ children, title }: { children: ReactNode; title?: string }) {
  const { colors, radius, shadow } = useTheme();
  const rows = (Array.isArray(children) ? children : [children]).filter(Boolean);
  return (
    <View style={{ gap: 8 }}>
      {title ? <Text variant="label" tone="subtle" style={{ paddingHorizontal: 4 }}>{title}</Text> : null}
      <View style={{ backgroundColor: colors.surface, borderRadius: radius.lg, borderWidth: 1, borderColor: colors.border, padding: 4, ...shadow.card }}>
        {rows.map((row, i) => (
          <View key={i}>
            {i > 0 ? <View style={{ height: 1, backgroundColor: colors.border, marginLeft: 64, marginRight: 10 }} /> : null}
            {row}
          </View>
        ))}
      </View>
    </View>
  );
}
