import { LinearGradient } from 'expo-linear-gradient';
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
  accessibilityLabel?: string;
};

export function Card({ children, onPress, tone = 'default', padded = true, style, testID, accessibilityLabel }: CardProps) {
  const { colors, radius, spacing, shadow } = useTheme();

  if (tone === 'brand') {
    return (
      <HeroSurface style={{ padding: padded ? spacing.xl : 0, ...style }} testID={testID}>
        {children}
      </HeroSurface>
    );
  }

  const base: ViewStyle = {
    backgroundColor: tone === 'muted' ? colors.surfaceMuted : colors.surface,
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
      accessibilityLabel={accessibilityLabel}
      onPress={onPress}
      style={({ pressed }) => [base, { transform: [{ scale: pressed ? 0.99 : 1 }], opacity: pressed ? 0.96 : 1 }, style]}
    >
      {children}
    </Pressable>
  );
}

/** The brand gradient panel (hero cards, welcome, celebration). Content on it uses the `onHero` tokens. */
export function HeroSurface({ children, style, testID, rounded = true }: { children: ReactNode; style?: ViewStyle; testID?: string; rounded?: boolean }) {
  const { colors, radius } = useTheme();
  return (
    <LinearGradient
      testID={testID}
      colors={[colors.heroFrom, colors.heroTo]}
      start={{ x: 0, y: 0 }}
      end={{ x: 1, y: 1 }}
      style={[{ borderRadius: rounded ? radius.xl : 0, overflow: 'hidden' }, style]}
    >
      {/* Soft decorative circles: depth without an image asset. */}
      <View pointerEvents="none" style={{ position: 'absolute', width: 220, height: 220, borderRadius: 110, right: -70, top: -90, backgroundColor: colors.onHeroTint }} />
      <View pointerEvents="none" style={{ position: 'absolute', width: 140, height: 140, borderRadius: 70, right: 40, bottom: -80, backgroundColor: colors.onHeroTint, opacity: 0.6 }} />
      {children}
    </LinearGradient>
  );
}
