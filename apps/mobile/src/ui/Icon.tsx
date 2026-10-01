import { Ionicons } from '@expo/vector-icons';
import type { ComponentProps } from 'react';
import type { ColorValue } from 'react-native';

import { type TextTone, useTheme } from './theme';

export type IconName = ComponentProps<typeof Ionicons>['name'];

type IconProps = {
  name: IconName;
  size?: number;
  tone?: TextTone | 'accent' | 'info';
  color?: ColorValue;
};

export function Icon({ name, size = 20, tone = 'default', color }: IconProps) {
  const { colors } = useTheme();
  const map: Record<string, ColorValue> = {
    default: colors.text,
    muted: colors.textMuted,
    subtle: colors.textSubtle,
    primary: colors.primary,
    onPrimary: colors.onPrimary,
    success: colors.success,
    warning: colors.warning,
    danger: colors.danger,
    accent: colors.accent,
    info: colors.info,
  };
  return <Ionicons name={name} size={size} color={color ?? map[tone]} />;
}
