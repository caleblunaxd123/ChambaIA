import { zodResolver } from '@hookform/resolvers/zod';
import { Link } from 'expo-router';
import { useForm } from 'react-hook-form';
import { View } from 'react-native';

import { AuthLayout } from '@/features/auth/AuthLayout';
import { type RegisterForm, registerSchema } from '@/features/auth/schemas';
import { useRegister } from '@/features/auth/useAuthActions';
import { haptics } from '@/lib/haptics';
import { Button } from '@/ui/Button';
import { Card } from '@/ui/Card';
import { FormInput } from '@/ui/Input';
import { Text } from '@/ui/Text';

export default function RegisterScreen() {
  const register = useRegister();
  const { control, handleSubmit } = useForm<RegisterForm>({
    resolver: zodResolver(registerSchema),
    defaultValues: { fullName: '', email: '', password: '' },
  });

  const submit = handleSubmit((values) =>
    register.mutate(values, { onError: () => haptics.error(), onSuccess: () => haptics.success() }),
  );

  return (
    <AuthLayout title="Crea tu cuenta" subtitle="Cuéntanos quién eres y tu agente empieza a buscar por ti.">
      <View style={{ gap: 16 }}>
        <FormInput control={control} name="fullName" label="Tu nombre" icon="person-outline" placeholder="Areli Quispe" autoCapitalize="words" autoComplete="name" textContentType="name" />
        <FormInput control={control} name="email" label="Correo" icon="mail-outline" placeholder="tu@correo.com" autoCapitalize="none" autoComplete="email" keyboardType="email-address" textContentType="emailAddress" />
        <FormInput control={control} name="password" label="Contraseña" icon="lock-closed-outline" placeholder="Mínimo 8 caracteres" hint="Usa letras y al menos un número." secureTextEntry autoComplete="new-password" textContentType="newPassword" onSubmitEditing={submit} />

        {register.error ? (
          <Card tone="muted">
            <Text tone="danger" testID="register-error">{register.error.message}</Text>
          </Card>
        ) : null}

        <Button label="Crear cuenta" onPress={submit} loading={register.isPending} fullWidth testID="register-submit" />
        <Text variant="caption" tone="subtle" style={{ textAlign: 'center' }}>
          Guardamos tu información de forma segura y solo la usamos para encontrarte empleo. Puedes eliminarla cuando quieras.
        </Text>
      </View>

      <View style={{ flexDirection: 'row', justifyContent: 'center', gap: 6 }}>
        <Text tone="muted">¿Ya tienes cuenta?</Text>
        <Link href="/login" replace>
          <Text tone="primary" style={{ fontFamily: 'PlusJakartaSans_700Bold' }}>Inicia sesión</Text>
        </Link>
      </View>
    </AuthLayout>
  );
}
