import { View } from 'react-native';

import { Icon, type IconName } from './Icon';
import { Text } from './Text';
import { useTheme } from './theme';

export type BadgeTone = 'neutral' | 'brand' | 'success' | 'info' | 'warning' | 'danger' | 'accent';

type BadgeProps = {
  label: string;
  tone?: BadgeTone;
  icon?: IconName;
  size?: 'md' | 'sm';
};

export function useBadgeColors(tone: BadgeTone) {
  const { colors } = useTheme();
  return {
    neutral: { bg: colors.surfaceMuted, fg: colors.textMuted },
    brand: { bg: colors.primaryTint, fg: colors.primary },
    success: { bg: colors.successTint, fg: colors.success },
    info: { bg: colors.infoTint, fg: colors.info },
    warning: { bg: colors.warningTint, fg: colors.warning },
    danger: { bg: colors.dangerTint, fg: colors.danger },
    accent: { bg: colors.accentTint, fg: colors.warning },
  }[tone];
}

export function Badge({ label, tone = 'neutral', icon, size = 'md' }: BadgeProps) {
  const { radius } = useTheme();
  const { bg, fg } = useBadgeColors(tone);
  return (
    <View
      style={{
        flexDirection: 'row',
        alignItems: 'center',
        alignSelf: 'flex-start',
        gap: 4,
        backgroundColor: bg,
        borderRadius: radius.pill,
        paddingHorizontal: size === 'md' ? 10 : 8,
        paddingVertical: size === 'md' ? 5 : 3,
      }}
    >
      {icon ? <Icon name={icon} size={size === 'md' ? 14 : 12} color={fg} /> : null}
      <Text variant="caption" style={{ color: fg, fontSize: size === 'md' ? 12.5 : 11.5, lineHeight: 16 }}>
        {label}
      </Text>
    </View>
  );
}
