import type { ReactNode } from 'react';
import { RefreshControl, ScrollView, View, type ViewStyle } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useTheme } from './theme';

type ScreenProps = {
  children: ReactNode;
  /** Pull-to-refresh handler; shows the native spinner while `refreshing` is true. */
  onRefresh?: () => void;
  refreshing?: boolean;
  /** Extra bottom space, e.g. to clear a tab bar. */
  bottomInset?: number;
  scroll?: boolean;
  /** Disable the top safe-area padding when a native header is already shown. */
  topInset?: boolean;
  contentStyle?: ViewStyle;
};

export const SCREEN_MAX_WIDTH = 720;

/** Standard page: safe areas, background, centred max-width column (so web/tablet stay readable), optional pull-to-refresh. */
export function Screen({ children, onRefresh, refreshing = false, bottomInset = 24, scroll = true, topInset = true, contentStyle }: ScreenProps) {
  const { colors, spacing } = useTheme();
  const insets = useSafeAreaInsets();

  const content: ViewStyle = {
    width: '100%',
    maxWidth: SCREEN_MAX_WIDTH,
    alignSelf: 'center',
    paddingHorizontal: spacing.lg,
    paddingTop: (topInset ? insets.top : 0) + spacing.md,
    paddingBottom: bottomInset,
    gap: spacing.lg,
    ...contentStyle,
  };

  if (!scroll) {
    return <View style={{ flex: 1, backgroundColor: colors.bg }}><View style={[{ flex: 1 }, content]}>{children}</View></View>;
  }

  return (
    <ScrollView
      style={{ flex: 1, backgroundColor: colors.bg }}
      contentContainerStyle={content}
      keyboardShouldPersistTaps="handled"
      showsVerticalScrollIndicator={false}
      refreshControl={onRefresh ? <RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} colors={[colors.primary]} /> : undefined}
    >
      {children}
    </ScrollView>
  );
}
