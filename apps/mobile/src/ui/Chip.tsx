import { Pressable, ScrollView, View } from 'react-native';

import { haptics } from '@/lib/haptics';

import { Icon, type IconName } from './Icon';
import { Text } from './Text';
import { fontFamily, useTheme } from './theme';

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

  const body = (pressed: boolean) => ({
    flexDirection: 'row' as const,
    alignItems: 'center' as const,
    gap: 6,
    paddingLeft: 14,
    paddingRight: onRemove ? 8 : 14,
    paddingVertical: 8,
    borderRadius: radius.pill,
    borderWidth: 1,
    borderColor: selected ? colors.primary : colors.border,
    backgroundColor: selected ? colors.primary : pressed ? colors.surfaceMuted : colors.surface,
  });

  const content = (
    <>
      {icon ? <Icon name={icon} size={15} color={fg} /> : null}
      <Text variant="caption" style={{ color: fg, fontFamily: selected ? fontFamily.bold : fontFamily.medium }}>{label}</Text>
    </>
  );

  // Removable chips are a plain container with one real button (the ×): no button nested inside a button.
  if (onRemove && !onPress) {
    return (
      <View testID={testID} style={body(false)}>
        {content}
        <Pressable
          onPress={() => {
            haptics.tap();
            onRemove();
          }}
          hitSlop={10}
          accessibilityRole="button"
          accessibilityLabel={`Quitar ${label}`}
        >
          <Icon name="close-circle" size={18} color={colors.textSubtle} />
        </Pressable>
      </View>
    );
  }

  return (
    <Pressable
      testID={testID}
      accessibilityRole="button"
      accessibilityState={{ selected }}
      disabled={!onPress}
      onPress={() => {
        haptics.tap();
        onPress?.();
      }}
      style={({ pressed }) => body(pressed)}
    >
      {content}
      {selected && onPress ? <Icon name="checkmark" size={15} color={fg} /> : null}
    </Pressable>
  );
}

type SegmentedTabsProps<T extends string> = {
  options: readonly { value: T; label: string; count?: number }[];
  value: T;
  onChange: (value: T) => void;
  scrollable?: boolean;
};

/** Pill-shaped tab switcher (feed tabs). */
export function SegmentedTabs<T extends string>({ options, value, onChange, scrollable = false }: SegmentedTabsProps<T>) {
  const { colors, radius, shadow } = useTheme();

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
          ...(active ? shadow.soft : null),
        }}
      >
        <Text variant="caption" tone={active ? 'default' : 'muted'} style={{ fontFamily: active ? fontFamily.bold : fontFamily.medium }}>
          {o.label}
        </Text>
        {o.count !== undefined && o.count > 0 ? (
          <View style={{ minWidth: 20, height: 20, borderRadius: 10, paddingHorizontal: 5, backgroundColor: active ? colors.primary : colors.border, alignItems: 'center', justifyContent: 'center' }}>
            <Text variant="label" style={{ color: active ? colors.onPrimary : colors.textMuted, fontSize: 10.5, letterSpacing: 0 }}>{o.count > 99 ? '99+' : o.count}</Text>
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
