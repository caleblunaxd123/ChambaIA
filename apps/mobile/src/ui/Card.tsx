import type { ReactNode } from 'react';
import { Pressable, View, type ViewStyle } from 'react-native';

import { useTheme } from './theme';

type CardProps = {
  children: ReactNode;
  onPress?: () => void;
  tone?: 'default' | 'muted' | 'brand';
  padded?: boolean;
  style?: ViewStyle;
  testID?: string;
};

export function Card({ children, onPress, tone = 'default', padded = true, style, testID }: CardProps) {
  const { colors, radius, spacing, shadow } = useTheme();
  const base: ViewStyle = {
    backgroundColor: tone === 'brand' ? colors.primary : tone === 'muted' ? colors.surfaceMuted : colors.surface,
    borderRadius: radius.lg,
    padding: padded ? spacing.lg : 0,
    borderWidth: tone === 'default' ? 1 : 0,
    borderColor: colors.border,
    ...(tone === 'default' ? shadow.card : null),
  };

  if (!onPress) {
    return (
      <View testID={testID} style={[base, style]}>
        {children}
      </View>
    );
  }
  return (
    <Pressable
      testID={testID}
      accessibilityRole="button"
      onPress={onPress}
      style={({ pressed }) => [base, { transform: [{ scale: pressed ? 0.99 : 1 }] }, style]}
    >
      {children}
    </Pressable>
  );
}
