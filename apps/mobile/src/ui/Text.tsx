import { Text as RNText, type TextProps as RNTextProps } from 'react-native';

import { type TextTone, type TextVariant, useTheme } from './theme';

export type TextProps = RNTextProps & {
  variant?: TextVariant;
  tone?: TextTone;
};

export function Text({ variant = 'body', tone = 'default', style, ...rest }: TextProps) {
  const { colors, typography } = useTheme();
  const color: Record<TextTone, string> = {
    default: colors.text,
    muted: colors.textMuted,
    subtle: colors.textSubtle,
    primary: colors.primary,
    onPrimary: colors.onPrimary,
    success: colors.success,
    warning: colors.warning,
    danger: colors.danger,
  };
  return (
    <RNText
      {...rest}
      style={[typography[variant], { color: color[tone] }, variant === 'label' && { textTransform: 'uppercase' }, style]}
    />
  );
}
