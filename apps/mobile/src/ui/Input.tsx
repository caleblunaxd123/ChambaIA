import { useState } from 'react';
import { Pressable, TextInput, View, type TextInputProps } from 'react-native';
import { type Control, type FieldPath, type FieldValues, useController } from 'react-hook-form';

import { Icon, type IconName } from './Icon';
import { Text } from './Text';
import { fontFamily, useTheme } from './theme';

type InputProps = Omit<TextInputProps, 'style'> & {
  label?: string;
  error?: string;
  hint?: string;
  icon?: IconName;
  suffix?: string;
};

export function Input({ label, error, hint, icon, suffix, secureTextEntry, ...rest }: InputProps) {
  const { colors, radius } = useTheme();
  const [focused, setFocused] = useState(false);
  const [hidden, setHidden] = useState(true);
  const isPassword = secureTextEntry === true;

  return (
    <View style={{ gap: 6 }}>
      {label ? <Text variant="caption" tone="muted">{label}</Text> : null}
      <View
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          gap: 10,
          backgroundColor: colors.surface,
          borderRadius: radius.md,
          borderWidth: 1.5,
          borderColor: error ? colors.danger : focused ? colors.primary : colors.border,
          paddingHorizontal: 14,
          minHeight: 52,
        }}
      >
        {icon ? <Icon name={icon} size={19} tone="subtle" /> : null}
        <TextInput
          {...rest}
          secureTextEntry={isPassword && hidden}
          onFocus={(e) => {
            setFocused(true);
            rest.onFocus?.(e);
          }}
          onBlur={(e) => {
            setFocused(false);
            rest.onBlur?.(e);
          }}
          placeholderTextColor={colors.textSubtle}
          style={{
            flex: 1,
            color: colors.text,
            fontFamily: fontFamily.medium,
            fontSize: 15.5,
            paddingVertical: 12,
            outlineStyle: 'solid',
            outlineWidth: 0,
          }}
        />
        {suffix ? <Text variant="caption" tone="subtle">{suffix}</Text> : null}
        {isPassword ? (
          <Pressable accessibilityLabel={hidden ? 'Mostrar contraseña' : 'Ocultar contraseña'} onPress={() => setHidden((h) => !h)} hitSlop={10}>
            <Icon name={hidden ? 'eye-outline' : 'eye-off-outline'} size={20} tone="subtle" />
          </Pressable>
        ) : null}
      </View>
      {error ? (
        <Text variant="caption" tone="danger">{error}</Text>
      ) : hint ? (
        <Text variant="caption" tone="subtle">{hint}</Text>
      ) : null}
    </View>
  );
}

type FormInputProps<T extends FieldValues> = Omit<InputProps, 'value' | 'onChangeText' | 'error'> & {
  control: Control<T>;
  name: FieldPath<T>;
};

/** Input bound to react-hook-form: shows the validation message of the field automatically. */
export function FormInput<T extends FieldValues>({ control, name, ...rest }: FormInputProps<T>) {
  const { field, fieldState } = useController({ control, name });
  return (
    <Input
      {...rest}
      value={field.value === undefined || field.value === null ? '' : String(field.value)}
      onChangeText={field.onChange}
      onBlur={field.onBlur}
      error={fieldState.error?.message}
    />
  );
}
