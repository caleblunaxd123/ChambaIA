import { View } from 'react-native';

import { useTheme } from './theme';

type Segment = { value: number; color: string };

/** Single value (0..1) or a stacked bar of segments that share the full width proportionally. */
export function ProgressBar({ value, segments, height = 8, track, color }: { value?: number; segments?: Segment[]; height?: number; track?: string; color?: string }) {
  const { colors, radius } = useTheme();
  const parts: Segment[] = segments ?? [{ value: Math.max(0, Math.min(1, value ?? 0)), color: color ?? colors.primary }];
  const total = segments ? Math.max(1, parts.reduce((sum, s) => sum + s.value, 0)) : 1;

  return (
    <View
      accessibilityRole="progressbar"
      accessibilityValue={segments ? undefined : { min: 0, max: 100, now: Math.round((value ?? 0) * 100) }}
      style={{ height, borderRadius: radius.pill, backgroundColor: track ?? colors.surfaceMuted, flexDirection: 'row', overflow: 'hidden', gap: segments ? 2 : 0 }}
    >
      {parts.map((s, i) =>
        s.value > 0 ? <View key={i} style={{ width: `${(s.value / total) * 100}%`, backgroundColor: s.color, borderRadius: radius.pill }} /> : null,
      )}
    </View>
  );
}
