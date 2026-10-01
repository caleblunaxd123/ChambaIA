import { ActivityIndicator, Pressable, StyleSheet, View, type ViewStyle } from 'react-native';

import { haptics } from '@/lib/haptics';

import { Icon, type IconName } from './Icon';
import { Text } from './Text';
import { useTheme } from './theme';

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger';
type Size = 'md' | 'sm';

type ButtonProps = {
  label: string;
  onPress?: () => void;
  variant?: Variant;
  size?: Size;
  icon?: IconName;
  loading?: boolean;
  disabled?: boolean;
  fullWidth?: boolean;
  style?: ViewStyle;
  testID?: string;
};

export function Button({
  label,
  onPress,
  variant = 'primary',
  size = 'md',
  icon,
  loading = false,
  disabled = false,
  fullWidth = false,
  style,
  testID,
}: ButtonProps) {
  const { colors, radius, spacing } = useTheme();
  const inactive = disabled || loading;

  const palette = {
    primary: { bg: colors.primary, pressed: colors.primaryPressed, fg: colors.onPrimary, border: 'transparent' },
    secondary: { bg: colors.surface, pressed: colors.surfaceMuted, fg: colors.text, border: colors.borderStrong },
    ghost: { bg: 'transparent', pressed: colors.surfaceMuted, fg: colors.primary, border: 'transparent' },
    danger: { bg: colors.dangerTint, pressed: '#F9D7DB', fg: colors.danger, border: 'transparent' },
  }[variant];

  return (
    <Pressable
      testID={testID}
      accessibilityRole="button"
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
          opacity: inactive && !loading ? 0.5 : 1,
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
            {icon ? <Icon name={icon} size={size === 'md' ? 20 : 17} color={palette.fg} /> : null}
            <Text variant={size === 'md' ? 'bodyStrong' : 'caption'} style={{ color: palette.fg }}>
              {label}
            </Text>
          </>
        )}
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  base: { alignItems: 'center', justifyContent: 'center', borderWidth: 1 },
  content: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8 },
});
