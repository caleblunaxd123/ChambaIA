import { useState } from 'react';
import { Pressable, Switch, View } from 'react-native';

import { haptics } from '@/lib/haptics';

import { Chip } from './Chip';
import { Icon } from './Icon';
import { Input } from './Input';
import { Text } from './Text';
import { useTheme } from './theme';

type TagInputProps = {
  label: string;
  values: string[];
  onChange: (values: string[]) => void;
  placeholder?: string;
  hint?: string;
  max?: number;
};

/** Free-text list editor: type, press add (or enter), remove with the chip's ×. */
export function TagInput({ label, values, onChange, placeholder, hint, max = 40 }: TagInputProps) {
  const { colors } = useTheme();
  const [draft, setDraft] = useState('');

  const add = () => {
    const value = draft.trim();
    if (!value || values.length >= max || values.some((v) => v.toLowerCase() === value.toLowerCase())) {
      setDraft('');
      return;
    }
    haptics.tap();
    onChange([...values, value]);
    setDraft('');
  };

  return (
    <View style={{ gap: 8 }}>
      <View style={{ flexDirection: 'row', alignItems: 'flex-end', gap: 8 }}>
        <View style={{ flex: 1 }}>
          <Input label={label} value={draft} onChangeText={setDraft} placeholder={placeholder} hint={hint} onSubmitEditing={add} returnKeyType="done" />
        </View>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={`Agregar a ${label}`}
          onPress={add}
          style={{ width: 52, height: 52, borderRadius: 14, backgroundColor: draft.trim() ? colors.primary : colors.surfaceMuted, alignItems: 'center', justifyContent: 'center', marginBottom: hint ? 22 : 0 }}
        >
          <Icon name="add" size={24} color={draft.trim() ? colors.onPrimary : colors.textSubtle} />
        </Pressable>
      </View>
      {values.length > 0 ? (
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
          {values.map((v) => (
            <Chip key={v} label={v} onRemove={() => onChange(values.filter((x) => x !== v))} />
          ))}
        </View>
      ) : null}
    </View>
  );
}

type ToggleRowProps = {
  label: string;
  description?: string;
  value: boolean;
  onChange: (value: boolean) => void;
};

export function ToggleRow({ label, description, value, onChange }: ToggleRowProps) {
  const { colors } = useTheme();
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
      <View style={{ flex: 1 }}>
        <Text variant="bodyStrong">{label}</Text>
        {description ? <Text variant="caption" tone="muted">{description}</Text> : null}
      </View>
      <Switch
        accessibilityLabel={label}
        value={value}
        onValueChange={(v) => {
          haptics.tap();
          onChange(v);
        }}
        trackColor={{ false: colors.borderStrong, true: colors.primary }}
        thumbColor="#FFFFFF"
      />
    </View>
  );
}
