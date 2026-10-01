import { Pressable, ScrollView, View } from 'react-native';

import { haptics } from '@/lib/haptics';

import { Icon, type IconName } from './Icon';
import { Text } from './Text';
import { useTheme } from './theme';

type ChipProps = {
  label: string;
  selected?: boolean;
  onPress?: () => void;
  onRemove?: () => void;
  icon?: IconName;
  testID?: string;
};

/** Selectable / removable pill. Used for filters, preferences and skills. */
export function Chip({ label, selected = false, onPress, onRemove, icon, testID }: ChipProps) {
  const { colors, radius } = useTheme();
  const fg = selected ? colors.onPrimary : colors.text;
  return (
    <Pressable
      testID={testID}
      accessibilityRole={onPress ? 'button' : undefined}
      accessibilityState={{ selected }}
      disabled={!onPress && !onRemove}
      onPress={() => {
        haptics.tap();
        onPress?.();
      }}
      style={({ pressed }) => ({
        flexDirection: 'row',
        alignItems: 'center',
        gap: 6,
        paddingHorizontal: 14,
        paddingVertical: 8,
        borderRadius: radius.pill,
        borderWidth: 1,
        borderColor: selected ? colors.primary : colors.border,
        backgroundColor: selected ? colors.primary : pressed ? colors.surfaceMuted : colors.surface,
      })}
    >
      {icon ? <Icon name={icon} size={15} color={fg} /> : null}
      <Text variant="caption" style={{ color: fg }}>{label}</Text>
      {onRemove ? (
        <Pressable onPress={onRemove} hitSlop={8} accessibilityLabel={`Quitar ${label}`}>
          <Icon name="close-circle" size={16} color={selected ? colors.onPrimary : colors.textSubtle} />
        </Pressable>
      ) : null}
    </Pressable>
  );
}

type SegmentedTabsProps<T extends string> = {
  options: readonly { value: T; label: string; count?: number }[];
  value: T;
  onChange: (value: T) => void;
  scrollable?: boolean;
};

/** Pill-shaped tab switcher (feed tabs, tracker stages). */
export function SegmentedTabs<T extends string>({ options, value, onChange, scrollable = false }: SegmentedTabsProps<T>) {
  const { colors, radius } = useTheme();

  const items = options.map((o) => {
    const active = o.value === value;
    return (
      <Pressable
        key={o.value}
        accessibilityRole="tab"
        accessibilityState={{ selected: active }}
        testID={`tab-${o.value}`}
        onPress={() => {
          if (!active) haptics.tap();
          onChange(o.value);
        }}
        style={{
          flex: scrollable ? undefined : 1,
          flexDirection: 'row',
          alignItems: 'center',
          justifyContent: 'center',
          gap: 6,
          paddingVertical: 9,
          paddingHorizontal: scrollable ? 16 : 8,
          borderRadius: radius.pill,
          backgroundColor: active ? colors.surface : 'transparent',
          boxShadow: active ? '0 1px 6px rgba(27, 36, 55, 0.12)' : undefined,
        }}
      >
        <Text variant="caption" tone={active ? 'default' : 'muted'} style={{ fontFamily: active ? 'PlusJakartaSans_700Bold' : undefined }}>
          {o.label}
        </Text>
        {o.count !== undefined && o.count > 0 ? (
          <View style={{ minWidth: 20, height: 20, borderRadius: 10, paddingHorizontal: 5, backgroundColor: active ? colors.primary : colors.borderStrong, alignItems: 'center', justifyContent: 'center' }}>
            <Text variant="label" style={{ color: colors.onPrimary, fontSize: 10.5, letterSpacing: 0 }}>{o.count}</Text>
          </View>
        ) : null}
      </Pressable>
    );
  });

  const container = { flexDirection: 'row', gap: 2, padding: 4, borderRadius: radius.pill, backgroundColor: colors.surfaceMuted } as const;

  if (scrollable) {
    return (
      <ScrollView horizontal showsHorizontalScrollIndicator={false} style={{ flexGrow: 0 }} contentContainerStyle={container}>
        {items}
      </ScrollView>
    );
  }
  return <View style={container}>{items}</View>;
}
