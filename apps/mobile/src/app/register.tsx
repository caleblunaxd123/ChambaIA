import { zodResolver } from '@hookform/resolvers/zod';
import { Link } from 'expo-router';
import { useForm, useWatch } from 'react-hook-form';
import { View } from 'react-native';

import { AuthLayout } from '@/features/auth/AuthLayout';
import { type RegisterForm, passwordChecks, registerSchema } from '@/features/auth/schemas';
import { useRegister } from '@/features/auth/useAuthActions';
import { haptics } from '@/lib/haptics';
import { Button } from '@/ui/Button';
import { InlineError } from '@/ui/EmptyState';
import { Icon } from '@/ui/Icon';
import { FormInput } from '@/ui/Input';
import { Text } from '@/ui/Text';
import { fontFamily } from '@/ui/theme';

export default function RegisterScreen() {
  const register = useRegister();
  const { control, handleSubmit } = useForm<RegisterForm>({
    resolver: zodResolver(registerSchema),
    defaultValues: { fullName: '', email: '', password: '' },
  });
  const password = useWatch({ control, name: 'password' });

  const submit = handleSubmit((values) =>
    register.mutate(values, { onError: () => haptics.error(), onSuccess: () => haptics.success() }),
  );

  return (
    <AuthLayout title="Crea tu cuenta" subtitle="En 2 minutos tu agente empieza a buscar por ti. Es gratis.">
      <View style={{ gap: 16 }}>
        <FormInput control={control} name="fullName" label="Tu nombre" icon="person-outline" placeholder="Ej.: Lucía Torres" autoCapitalize="words" autoComplete="name" textContentType="name" returnKeyType="next" />
        <FormInput control={control} name="email" label="Correo" icon="mail-outline" placeholder="tu@correo.com" autoCapitalize="none" autoComplete="email" keyboardType="email-address" textContentType="emailAddress" returnKeyType="next" />
        <View style={{ gap: 10 }}>
          <FormInput control={control} name="password" label="Contraseña" icon="lock-closed-outline" placeholder="Mínimo 8 caracteres" secureTextEntry autoComplete="new-password" textContentType="newPassword" returnKeyType="go" onSubmitEditing={submit} />
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', columnGap: 14, rowGap: 6 }} testID="password-checks">
            {passwordChecks(password ?? '').map((c) => (
              <View key={c.label} style={{ flexDirection: 'row', alignItems: 'center', gap: 4 }}>
                <Icon name={c.ok ? 'checkmark-circle' : 'ellipse-outline'} size={15} tone={c.ok ? 'success' : 'subtle'} />
                <Text variant="caption" tone={c.ok ? 'success' : 'subtle'}>{c.label}</Text>
              </View>
            ))}
          </View>
        </View>

        {register.error ? <InlineError message={register.error.message} testID="register-error" /> : null}

        <Button label="Crear cuenta" trailingIcon="arrow-forward" onPress={submit} loading={register.isPending} fullWidth testID="register-submit" />
        <View style={{ flexDirection: 'row', gap: 8, alignItems: 'flex-start', paddingHorizontal: 4 }}>
          <Icon name="shield-checkmark-outline" size={16} tone="subtle" />
          <Text variant="caption" tone="subtle" style={{ flex: 1 }}>
            Guardamos tu información de forma segura y solo la usamos para encontrarte empleo. Puedes eliminarla cuando quieras.
          </Text>
        </View>
      </View>

      <View style={{ flexDirection: 'row', justifyContent: 'center', gap: 6 }}>
        <Text tone="muted">¿Ya tienes cuenta?</Text>
        <Link href="/login" replace>
          <Text tone="primary" style={{ fontFamily: fontFamily.bold }}>Inicia sesión</Text>
        </Link>
      </View>
    </AuthLayout>
  );
}
