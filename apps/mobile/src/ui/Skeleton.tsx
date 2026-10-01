import { useEffect, useState } from 'react';
import { Animated, View, type DimensionValue, type ViewStyle } from 'react-native';

import { Card } from './Card';
import { useTheme } from './theme';

type SkeletonProps = {
  width?: DimensionValue;
  height?: number;
  radius?: number;
  style?: ViewStyle;
};

/** Pulsing placeholder shown while data loads. Calm on purpose: no flashing shimmer. */
export function Skeleton({ width = '100%', height = 14, radius = 8, style }: SkeletonProps) {
  const { colors } = useTheme();
  const [opacity] = useState(() => new Animated.Value(0.55));

  useEffect(() => {
    const loop = Animated.loop(
      Animated.sequence([
        Animated.timing(opacity, { toValue: 1, duration: 800, useNativeDriver: true }),
        Animated.timing(opacity, { toValue: 0.55, duration: 800, useNativeDriver: true }),
      ]),
    );
    loop.start();
    return () => loop.stop();
  }, [opacity]);

  return <Animated.View style={[{ width, height, borderRadius: radius, backgroundColor: colors.border, opacity }, style]} />;
}

export function JobCardSkeleton() {
  return (
    <Card testID="job-card-skeleton">
      <View style={{ gap: 12 }}>
        <Skeleton width="30%" height={22} radius={11} />
        <Skeleton width="75%" height={18} />
        <Skeleton width="50%" height={14} />
        <View style={{ flexDirection: 'row', gap: 8 }}>
          <Skeleton width={80} height={24} radius={12} />
          <Skeleton width={96} height={24} radius={12} />
        </View>
      </View>
    </Card>
  );
}

export function JobListSkeleton({ count = 3 }: { count?: number }) {
  return (
    <View style={{ gap: 12 }}>
      {Array.from({ length: count }, (_, i) => (
        <JobCardSkeleton key={i} />
      ))}
    </View>
  );
}
