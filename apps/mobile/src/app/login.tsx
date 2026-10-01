import { zodResolver } from '@hookform/resolvers/zod';
import { Link } from 'expo-router';
import { useForm } from 'react-hook-form';
import { View } from 'react-native';

import { AuthLayout } from '@/features/auth/AuthLayout';
import { type LoginForm, loginSchema } from '@/features/auth/schemas';
import { useLogin } from '@/features/auth/useAuthActions';
import { haptics } from '@/lib/haptics';
import { Button } from '@/ui/Button';
import { Card } from '@/ui/Card';
import { FormInput } from '@/ui/Input';
import { Text } from '@/ui/Text';

const demoEmail = process.env.EXPO_PUBLIC_DEMO_EMAIL;
const demoPassword = process.env.EXPO_PUBLIC_DEMO_PASSWORD;

export default function LoginScreen() {
  const login = useLogin();
  const { control, handleSubmit } = useForm<LoginForm>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });

  const submit = handleSubmit((values) =>
    login.mutate(values, { onError: () => haptics.error() }),
  );

  return (
    <AuthLayout title="Bienvenida de vuelta" subtitle="Tu agente siguió buscando mientras no estabas.">
      <View style={{ gap: 16 }}>
        <FormInput control={control} name="email" label="Correo" icon="mail-outline" placeholder="tu@correo.com" autoCapitalize="none" autoComplete="email" keyboardType="email-address" textContentType="emailAddress" />
        <FormInput control={control} name="password" label="Contraseña" icon="lock-closed-outline" placeholder="Tu contraseña" secureTextEntry autoComplete="current-password" textContentType="password" onSubmitEditing={submit} />

        {login.error ? (
          <Card tone="muted">
            <Text tone="danger" testID="login-error">{login.error.message}</Text>
          </Card>
        ) : null}

        <Button label="Entrar" onPress={submit} loading={login.isPending} fullWidth testID="login-submit" />

        {__DEV__ && demoEmail && demoPassword ? (
          <Button
            label="Entrar con la cuenta demo (Areli)"
            variant="secondary"
            icon="flask-outline"
            fullWidth
            onPress={() => login.mutate({ email: demoEmail, password: demoPassword })}
            testID="login-demo"
          />
        ) : null}
      </View>

      <View style={{ flexDirection: 'row', justifyContent: 'center', gap: 6 }}>
        <Text tone="muted">¿Aún no tienes cuenta?</Text>
        <Link href="/register" replace>
          <Text tone="primary" style={{ fontFamily: 'PlusJakartaSans_700Bold' }}>Créala gratis</Text>
        </Link>
      </View>
    </AuthLayout>
  );
}
