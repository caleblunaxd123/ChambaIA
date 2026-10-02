import { ActivityIndicator, Pressable, StyleSheet, View, type ViewStyle } from 'react-native';

import { haptics } from '@/lib/haptics';

import { Icon, type IconName } from './Icon';
import { Text } from './Text';
import { fontFamily, type ThemeColors, useTheme } from './theme';

export type ButtonVariant = 'primary' | 'secondary' | 'tonal' | 'ghost' | 'danger' | 'onHero';
type Size = 'md' | 'sm';

type ButtonProps = {
  label: string;
  onPress?: () => void;
  variant?: ButtonVariant;
  size?: Size;
  icon?: IconName;
  /** Icon after the label (e.g. an arrow on "continue" buttons). */
  trailingIcon?: IconName;
  loading?: boolean;
  disabled?: boolean;
  fullWidth?: boolean;
  style?: ViewStyle;
  testID?: string;
  accessibilityLabel?: string;
};

function paletteFor(variant: ButtonVariant, colors: ThemeColors) {
  return {
    primary: { bg: colors.primary, pressed: colors.primaryPressed, fg: colors.onPrimary, border: 'transparent' },
    secondary: { bg: colors.surface, pressed: colors.surfaceMuted, fg: colors.text, border: colors.borderStrong },
    tonal: { bg: colors.primaryTint, pressed: colors.border, fg: colors.primary, border: 'transparent' },
    ghost: { bg: 'transparent', pressed: colors.surfaceMuted, fg: colors.primary, border: 'transparent' },
    danger: { bg: colors.dangerTint, pressed: colors.dangerPressed, fg: colors.danger, border: 'transparent' },
    onHero: { bg: colors.onHero, pressed: colors.primaryTint, fg: colors.heroFrom, border: 'transparent' },
  }[variant];
}

export function Button({
  label,
  onPress,
  variant = 'primary',
  size = 'md',
  icon,
  trailingIcon,
  loading = false,
  disabled = false,
  fullWidth = false,
  style,
  testID,
  accessibilityLabel,
}: ButtonProps) {
  const { colors, radius, spacing } = useTheme();
  const inactive = disabled || loading;
  const palette = paletteFor(variant, colors);
  const iconSize = size === 'md' ? 20 : 17;

  return (
    <Pressable
      testID={testID}
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel ?? label}
      accessibilityState={{ disabled: inactive, busy: loading }}
      disabled={inactive}
      onPress={() => {
        haptics.tap();
        onPress?.();
      }}
      style={({ pressed }) => [
        styles.base,
        {
          backgroundColor: pressed ? palette.pressed : palette.bg,
          borderColor: palette.border,
          borderRadius: radius.pill,
          paddingHorizontal: size === 'md' ? spacing.xl : spacing.lg,
          minHeight: size === 'md' ? 52 : 38,
          opacity: inactive && !loading ? 0.45 : 1,
          alignSelf: fullWidth ? 'stretch' : 'flex-start',
          transform: [{ scale: pressed ? 0.98 : 1 }],
        },
        style,
      ]}
    >
      <View style={styles.content}>
        {loading ? (
          <ActivityIndicator size="small" color={palette.fg} />
        ) : (
          <>
            {icon ? <Icon name={icon} size={iconSize} color={palette.fg} /> : null}
            <Text variant={size === 'md' ? 'bodyStrong' : 'caption'} style={{ color: palette.fg, fontFamily: fontFamily.bold }} numberOfLines={1}>
              {label}
            </Text>
            {trailingIcon ? <Icon name={trailingIcon} size={iconSize} color={palette.fg} /> : null}
          </>
        )}
      </View>
    </Pressable>
  );
}

type IconButtonProps = {
  icon: IconName;
  /** Required: icon-only controls must still be announced by screen readers. */
  label: string;
  onPress?: () => void;
  variant?: 'secondary' | 'tonal' | 'ghost' | 'primary' | 'onHero';
  size?: number;
  disabled?: boolean;
  selected?: boolean;
  testID?: string;
};

/** Round icon-only button (back, close, share, bookmark…). */
export function IconButton({ icon, label, onPress, variant = 'secondary', size = 44, disabled, selected, testID }: IconButtonProps) {
  const { colors } = useTheme();
  const palette = {
    secondary: { bg: colors.surface, pressed: colors.surfaceMuted, fg: colors.text, border: colors.border },
    tonal: { bg: colors.primaryTint, pressed: colors.border, fg: colors.primary, border: 'transparent' },
    ghost: { bg: 'transparent', pressed: colors.surfaceMuted, fg: colors.textMuted, border: 'transparent' },
    primary: { bg: colors.primary, pressed: colors.primaryPressed, fg: colors.onPrimary, border: 'transparent' },
    onHero: { bg: colors.onHeroTint, pressed: colors.onHeroTint, fg: colors.onHero, border: 'transparent' },
  }[selected ? 'primary' : variant];

  return (
    <Pressable
      testID={testID}
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled, selected }}
      disabled={disabled}
      hitSlop={6}
      onPress={() => {
        haptics.tap();
        onPress?.();
      }}
      style={({ pressed }) => ({
        width: size,
        height: size,
        borderRadius: size / 2,
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: pressed ? palette.pressed : palette.bg,
        borderWidth: palette.border === 'transparent' ? 0 : 1,
        borderColor: palette.border,
        opacity: disabled ? 0.45 : 1,
        transform: [{ scale: pressed ? 0.95 : 1 }],
      })}
    >
      <Icon name={icon} size={Math.round(size * 0.48)} color={palette.fg} />
    </Pressable>
  );
}

const styles = StyleSheet.create({
  base: { alignItems: 'center', justifyContent: 'center', borderWidth: 1 },
  content: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8 },
});
