import { useEffect, useState } from 'react';
import { Animated, View, type DimensionValue, type ViewStyle } from 'react-native';

import { Card } from './Card';
import { useTheme } from './theme';

type SkeletonProps = {
  width?: DimensionValue;
  height?: number;
  radius?: number;
  /** Placeholders on the brand gradient need a translucent white instead of the neutral grey. */
  onHero?: boolean;
  style?: ViewStyle;
};

/** Pulsing placeholder shown while data loads. Calm on purpose: no flashing shimmer. */
export function Skeleton({ width = '100%', height = 14, radius = 8, onHero = false, style }: SkeletonProps) {
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

  return <Animated.View style={[{ width, height, borderRadius: radius, backgroundColor: onHero ? colors.onHeroTint : colors.skeleton, opacity }, style]} />;
}

export function JobCardSkeleton() {
  return (
    <Card testID="job-card-skeleton">
      <View style={{ gap: 14 }}>
        <View style={{ flexDirection: 'row', gap: 12, alignItems: 'center' }}>
          <Skeleton width={44} height={44} radius={14} />
          <View style={{ flex: 1, gap: 8 }}>
            <Skeleton width="75%" height={16} />
            <Skeleton width="45%" height={12} />
          </View>
        </View>
        <View style={{ flexDirection: 'row', gap: 8 }}>
          <Skeleton width={120} height={24} radius={12} />
          <Skeleton width={70} height={24} radius={12} />
        </View>
        <Skeleton width="60%" height={12} />
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
