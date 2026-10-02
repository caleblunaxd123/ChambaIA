import { Pressable, View } from 'react-native';

import { Text } from './Text';
import { fontFamily } from './theme';

/** Section title with an optional "Ver todas"-style link on the right. */
export function SectionHeader({ title, subtitle, action, onAction, testID }: { title: string; subtitle?: string; action?: string; onAction?: () => void; testID?: string }) {
  return (
    <View style={{ flexDirection: 'row', alignItems: 'flex-end', justifyContent: 'space-between', gap: 12 }}>
      <View style={{ flex: 1, gap: 2 }}>
        <Text variant="title">{title}</Text>
        {subtitle ? <Text variant="caption" tone="muted">{subtitle}</Text> : null}
      </View>
      {action && onAction ? (
        <Pressable onPress={onAction} hitSlop={10} accessibilityRole="button" testID={testID} style={{ paddingVertical: 4 }}>
          <Text variant="caption" tone="primary" style={{ fontFamily: fontFamily.bold }}>{action}</Text>
        </Pressable>
      ) : null}
    </View>
  );
}
