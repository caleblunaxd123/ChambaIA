import { useRouter } from 'expo-router';
import { View } from 'react-native';

import { EmptyState } from '@/ui/EmptyState';
import { useTheme } from '@/ui/theme';

/** Broken or old links (e.g. an offer that expired) land here instead of a blank screen. */
export default function NotFoundScreen() {
  const router = useRouter();
  const { colors } = useTheme();
  return (
    <View style={{ flex: 1, justifyContent: 'center', backgroundColor: colors.bg }}>
      <EmptyState
        icon="compass-outline"
        title="Esta página no existe"
        message="Puede que la oferta ya no esté disponible o que el enlace esté incompleto."
        actionLabel="Ir al inicio"
        onAction={() => router.replace('/')}
      />
    </View>
  );
}
