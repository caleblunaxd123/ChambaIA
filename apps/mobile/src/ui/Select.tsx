import { useState } from 'react';
import { Pressable, View } from 'react-native';

import { haptics } from '@/lib/haptics';

import { BottomSheet } from './BottomSheet';
import { Button } from './Button';
import { Icon } from './Icon';
import { Text } from './Text';
import { useTheme } from './theme';

export type Option<T extends string | number> = { value: T; label: string; description?: string };

type SharedProps<T extends string | number> = {
  label?: string;
  placeholder?: string;
  options: readonly Option<T>[];
  title?: string;
  error?: string;
};

type SingleProps<T extends string | number> = SharedProps<T> & {
  value: T | null | undefined;
  onChange: (value: T | null) => void;
  /** Adds a "no preference" row that clears the value. */
  clearLabel?: string;
};

export function Select<T extends string | number>({ label, placeholder = 'Selecciona', options, title, error, value, onChange, clearLabel }: SingleProps<T>) {
  const [open, setOpen] = useState(false);
  const selected = options.find((o) => o.value === value);

  return (
    <>
      <Field label={label} error={error} text={selected?.label} placeholder={placeholder} onPress={() => setOpen(true)} />
      <BottomSheet visible={open} onClose={() => setOpen(false)} title={title ?? label}>
        <View style={{ gap: 4 }}>
          {clearLabel ? (
            <Row
              label={clearLabel}
              selected={value === null || value === undefined}
              onPress={() => {
                onChange(null);
                setOpen(false);
              }}
            />
          ) : null}
          {options.map((o) => (
            <Row
              key={String(o.value)}
              label={o.label}
              description={o.description}
              selected={o.value === value}
              onPress={() => {
                onChange(o.value);
                setOpen(false);
              }}
            />
          ))}
        </View>
      </BottomSheet>
    </>
  );
}

type MultiProps<T extends string | number> = SharedProps<T> & {
  value: readonly T[];
  onChange: (value: T[]) => void;
};

export function MultiSelect<T extends string | number>({ label, placeholder = 'Selecciona', options, title, error, value, onChange }: MultiProps<T>) {
  const [open, setOpen] = useState(false);
  const labels = options.filter((o) => value.includes(o.value)).map((o) => o.label);
  const summary = labels.length === 0 ? undefined : labels.length <= 2 ? labels.join(', ') : `${labels.length} seleccionados`;

  const toggle = (v: T) => onChange(value.includes(v) ? value.filter((x) => x !== v) : [...value, v]);

  return (
    <>
      <Field label={label} error={error} text={summary} placeholder={placeholder} onPress={() => setOpen(true)} />
      <BottomSheet
        visible={open}
        onClose={() => setOpen(false)}
        title={title ?? label}
        footer={<Button label="Listo" onPress={() => setOpen(false)} fullWidth />}
      >
        <View style={{ gap: 4 }}>
          {options.map((o) => (
            <Row key={String(o.value)} label={o.label} description={o.description} selected={value.includes(o.value)} multi onPress={() => toggle(o.value)} />
          ))}
        </View>
      </BottomSheet>
    </>
  );
}

function Field({ label, text, placeholder, error, onPress }: { label?: string; text?: string; placeholder: string; error?: string; onPress: () => void }) {
  const { colors, radius } = useTheme();
  return (
    <View style={{ gap: 6 }}>
      {label ? <Text variant="caption" tone="muted">{label}</Text> : null}
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={label}
        onPress={() => {
          haptics.tap();
          onPress();
        }}
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          justifyContent: 'space-between',
          minHeight: 52,
          paddingHorizontal: 14,
          borderRadius: radius.md,
          borderWidth: 1.5,
          borderColor: error ? colors.danger : colors.border,
          backgroundColor: colors.surface,
        }}
      >
        <Text variant="bodyStrong" tone={text ? 'default' : 'subtle'} numberOfLines={1} style={{ flex: 1 }}>
          {text ?? placeholder}
        </Text>
        <Icon name="chevron-down" size={18} tone="subtle" />
      </Pressable>
      {error ? <Text variant="caption" tone="danger">{error}</Text> : null}
    </View>
  );
}

function Row({ label, description, selected, multi, onPress }: { label: string; description?: string; selected: boolean; multi?: boolean; onPress: () => void }) {
  const { colors, radius } = useTheme();
  return (
    <Pressable
      accessibilityRole={multi ? 'checkbox' : 'radio'}
      accessibilityState={{ selected, checked: selected }}
      onPress={() => {
        haptics.tap();
        onPress();
      }}
      style={({ pressed }) => ({
        flexDirection: 'row',
        alignItems: 'center',
        gap: 12,
        paddingVertical: 12,
        paddingHorizontal: 12,
        borderRadius: radius.md,
        backgroundColor: selected ? colors.primaryTint : pressed ? colors.surfaceMuted : 'transparent',
      })}
    >
      <View style={{ flex: 1 }}>
        <Text variant="bodyStrong" tone={selected ? 'primary' : 'default'}>{label}</Text>
        {description ? <Text variant="caption" tone="muted">{description}</Text> : null}
      </View>
      {selected ? <Icon name={multi ? 'checkbox' : 'checkmark-circle'} size={22} tone="primary" /> : multi ? <Icon name="square-outline" size={22} tone="subtle" /> : null}
    </Pressable>
  );
}
