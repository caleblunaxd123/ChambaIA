import { Ionicons } from '@expo/vector-icons';
import type { ComponentProps } from 'react';
import type { ColorValue } from 'react-native';

import { toneColor } from './Text';
import { type TextTone, useTheme } from './theme';

export type IconName = ComponentProps<typeof Ionicons>['name'];

type IconProps = {
  name: IconName;
  size?: number;
  tone?: TextTone;
  color?: ColorValue;
};

export function Icon({ name, size = 20, tone = 'default', color }: IconProps) {
  const { colors } = useTheme();
  return <Ionicons name={name} size={size} color={color ?? toneColor(colors, tone)} />;
}
