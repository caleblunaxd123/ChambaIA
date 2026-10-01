import { type ReactNode, useEffect, useState } from 'react';
import { Animated, Dimensions, Modal, Pressable, ScrollView, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Icon } from './Icon';
import { Text } from './Text';
import { useTheme } from './theme';

type BottomSheetProps = {
  visible: boolean;
  onClose: () => void;
  title?: string;
  children: ReactNode;
  footer?: ReactNode;
};

/** Lightweight sheet built on RN Modal + Animated: no native bottom-sheet dependency to maintain. */
export function BottomSheet({ visible, onClose, title, children, footer }: BottomSheetProps) {
  const { colors, radius, spacing } = useTheme();
  const insets = useSafeAreaInsets();
  const [mounted, setMounted] = useState(visible);
  const [progress] = useState(() => new Animated.Value(0));
  const height = Dimensions.get('window').height;

  // Mount before animating in (state adjusted during render, the documented alternative to an effect).
  if (visible && !mounted) setMounted(true);

  useEffect(() => {
    if (visible) {
      Animated.timing(progress, { toValue: 1, duration: 240, useNativeDriver: true }).start();
    } else {
      Animated.timing(progress, { toValue: 0, duration: 180, useNativeDriver: true }).start(({ finished }) => {
        if (finished) setMounted(false);
      });
    }
  }, [visible, progress]);

  if (!mounted) return null;

  return (
    <Modal transparent visible animationType="none" onRequestClose={onClose} statusBarTranslucent>
      <View style={{ flex: 1, justifyContent: 'flex-end' }}>
        <Animated.View style={{ ...absoluteFill, backgroundColor: colors.overlay, opacity: progress }}>
          <Pressable style={{ flex: 1 }} onPress={onClose} accessibilityLabel="Cerrar" />
        </Animated.View>
        <Animated.View
          style={{
            backgroundColor: colors.surface,
            borderTopLeftRadius: radius.xl,
            borderTopRightRadius: radius.xl,
            maxHeight: height * 0.88,
            width: '100%',
            maxWidth: 640,
            alignSelf: 'center',
            paddingBottom: insets.bottom + spacing.lg,
            transform: [{ translateY: progress.interpolate({ inputRange: [0, 1], outputRange: [height * 0.5, 0] }) }],
          }}
        >
          <View style={{ alignItems: 'center', paddingTop: 10 }}>
            <View style={{ width: 40, height: 4, borderRadius: 2, backgroundColor: colors.borderStrong }} />
          </View>
          {title ? (
            <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingHorizontal: spacing.xl, paddingTop: spacing.md, paddingBottom: spacing.sm }}>
              <Text variant="title">{title}</Text>
              <Pressable onPress={onClose} hitSlop={12} accessibilityLabel="Cerrar">
                <Icon name="close" size={24} tone="muted" />
              </Pressable>
            </View>
          ) : null}
          <ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={{ paddingHorizontal: spacing.xl, paddingVertical: spacing.md, gap: spacing.lg }}>
            {children}
          </ScrollView>
          {footer ? <View style={{ paddingHorizontal: spacing.xl, paddingTop: spacing.sm }}>{footer}</View> : null}
        </Animated.View>
      </View>
    </Modal>
  );
}

const absoluteFill = { position: 'absolute', top: 0, left: 0, right: 0, bottom: 0 } as const;
