import { View } from 'react-native';

import { initials, pickIndex } from '@/lib/format';

import { Text } from './Text';
import { useTheme } from './theme';

/** Calm, distinguishable pairs (background tint + ink) for companies without a logo. Same name → same colour. */
const LIGHT = [
  ['#DDF2EF', '#0B6B66'],
  ['#E5EEFD', '#2A57B8'],
  ['#FFEDE2', '#B4501F'],
  ['#EEE7FB', '#6A3FB5'],
  ['#FFF1D6', '#8F5300'],
  ['#FCE7EF', '#A3285A'],
  ['#E3F3E2', '#2E7A2A'],
] as const;

const DARK = [
  ['#123432', '#5ED3C6'],
  ['#16243F', '#8DB5FF'],
  ['#3A2318', '#FFA77F'],
  ['#2A2140', '#C3A6FF'],
  ['#33280F', '#F2C46B'],
  ['#3A1A2A', '#FF8DB8'],
  ['#18301A', '#7FD67A'],
] as const;

type AvatarProps = {
  name: string;
  size?: number;
  /** Square-ish for companies, round for people. */
  shape?: 'rounded' | 'circle';
};

export function Avatar({ name, size = 44, shape = 'rounded' }: AvatarProps) {
  const { scheme } = useTheme();
  const palette = scheme === 'dark' ? DARK : LIGHT;
  const [bg, fg] = palette[pickIndex(name, palette.length)];
  return (
    <View
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
      style={{ width: size, height: size, borderRadius: shape === 'circle' ? size / 2 : size * 0.3, backgroundColor: bg, alignItems: 'center', justifyContent: 'center' }}
    >
      <Text variant="heading" style={{ color: fg, fontSize: size * 0.38, lineHeight: size * 0.46 }}>{initials(name)}</Text>
    </View>
  );
}
