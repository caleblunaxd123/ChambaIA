import { useEffect, useState } from 'react';
import { Animated, Pressable, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { haptics } from '@/lib/haptics';
import { type Toast, useToast } from '@/state/toast-store';

import { Icon, type IconName } from './Icon';
import { SCREEN_MAX_WIDTH } from './Screen';
import { Text } from './Text';
import { useTheme } from './theme';

const ICONS: Record<Toast['tone'], IconName> = { default: 'information-circle', success: 'checkmark-circle', danger: 'alert-circle' };

/** Renders the current toast at the top of the screen (clear of tab bars and action bars). Mounted once in the root layout. */
export function ToastHost() {
  const current = useToast((s) => s.current);
  const dismiss = useToast((s) => s.dismiss);
  const insets = useSafeAreaInsets();
  const { colors, radius, shadow } = useTheme();
  const [shown, setShown] = useState<Toast | null>(current);
  const [progress] = useState(() => new Animated.Value(0));

  // Keep the last toast on screen while it animates out (state adjusted during render, no effect needed).
  if (current && current !== shown) setShown(current);

  useEffect(() => {
    Animated.timing(progress, { toValue: current ? 1 : 0, duration: current ? 220 : 160, useNativeDriver: true }).start(({ finished }) => {
      if (finished && !current) setShown(null);
    });
    if (!current) return;
    const id = setTimeout(() => dismiss(current.id), current.durationMs);
    return () => clearTimeout(id);
  }, [current, dismiss, progress]);

  if (!shown) return null;

  const tint = shown.tone === 'success' ? colors.success : shown.tone === 'danger' ? colors.danger : colors.primary;

  return (
    <View pointerEvents="box-none" style={{ position: 'absolute', top: insets.top + 8, left: 0, right: 0, alignItems: 'center', paddingHorizontal: 12, zIndex: 1000 }}>
      <Animated.View
        accessibilityLiveRegion="polite"
        accessibilityRole="alert"
        testID="toast"
        style={{
          width: '100%',
          maxWidth: SCREEN_MAX_WIDTH - 32,
          opacity: progress,
          transform: [{ translateY: progress.interpolate({ inputRange: [0, 1], outputRange: [-24, 0] }) }],
          flexDirection: 'row',
          alignItems: 'center',
          gap: 10,
          paddingLeft: 14,
          paddingRight: shown.action ? 6 : 14,
          paddingVertical: 8,
          minHeight: 52,
          borderRadius: radius.lg,
          backgroundColor: colors.surfaceRaised,
          borderWidth: 1,
          borderColor: colors.border,
          ...shadow.raised,
        }}
      >
        <Icon name={ICONS[shown.tone]} size={22} color={tint} />
        <Text variant="bodyStrong" style={{ flex: 1 }} numberOfLines={2}>{shown.message}</Text>
        {shown.action ? (
          <Pressable
            accessibilityRole="button"
            testID="toast-action"
            onPress={() => {
              haptics.tap();
              shown.action?.onPress();
              dismiss(shown.id);
            }}
            style={({ pressed }) => ({ paddingHorizontal: 14, paddingVertical: 9, borderRadius: radius.pill, backgroundColor: pressed ? colors.primaryTint : 'transparent' })}
          >
            <Text variant="bodyStrong" tone="primary">{shown.action.label}</Text>
          </Pressable>
        ) : null}
      </Animated.View>
    </View>
  );
}
