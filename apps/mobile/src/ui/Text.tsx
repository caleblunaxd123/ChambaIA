import { Text as RNText, type TextProps as RNTextProps } from 'react-native';

import { type TextTone, type TextVariant, type ThemeColors, useTheme } from './theme';

export type TextProps = RNTextProps & {
  variant?: TextVariant;
  tone?: TextTone;
};

export function toneColor(colors: ThemeColors, tone: TextTone): string {
  return {
    default: colors.text,
    muted: colors.textMuted,
    subtle: colors.textSubtle,
    primary: colors.primary,
    onPrimary: colors.onPrimary,
    onHero: colors.onHero,
    success: colors.success,
    warning: colors.warning,
    danger: colors.danger,
    info: colors.info,
    accent: colors.accent,
  }[tone];
}

export function Text({ variant = 'body', tone = 'default', style, ...rest }: TextProps) {
  const { colors, typography } = useTheme();
  return (
    <RNText
      {...rest}
      style={[typography[variant], { color: toneColor(colors, tone) }, variant === 'label' && { textTransform: 'uppercase' }, style]}
    />
  );
}
