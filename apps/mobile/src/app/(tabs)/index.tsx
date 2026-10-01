import { useRouter } from 'expo-router';
import { Pressable, View } from 'react-native';

import { useFeed, useOverview, useProfile } from '@/api/queries';
import { JobCard } from '@/features/jobs/JobCard';
import { useJobActions } from '@/features/jobs/useJobActions';
import { firstName } from '@/lib/format';
import { useAuthStore } from '@/state/auth-store';
import { Button } from '@/ui/Button';
import { Card } from '@/ui/Card';
import { EmptyState, ErrorState } from '@/ui/EmptyState';
import { Icon } from '@/ui/Icon';
import { Screen } from '@/ui/Screen';
import { JobListSkeleton, Skeleton } from '@/ui/Skeleton';
import { Text } from '@/ui/Text';
import { useTheme } from '@/ui/theme';

export default function HomeScreen() {
  const router = useRouter();
  const { colors, radius } = useTheme();
  const user = useAuthStore((s) => s.user);
  const overview = useOverview();
  const profile = useProfile();
  const feed = useFeed('forYou', {});
  const { open, save, discard, busy } = useJobActions();

  const refreshing = overview.isRefetching || feed.isRefetching;
  const refresh = () => {
    void overview.refetch();
    void feed.refetch();
  };

  const items = feed.data?.pages.flatMap((p) => p.items).slice(0, 5) ?? [];
  const needsProfile = profile.data !== undefined && profile.data.skills.length === 0;
  const hasError = overview.isError && feed.isError;

  return (
    <Screen onRefresh={refresh} refreshing={refreshing}>
      <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
        <View style={{ gap: 2, flex: 1 }}>
          <Text variant="caption" tone="muted">Tu agente de empleo</Text>
          <Text variant="display" testID="home-greeting">Hola, {firstName(user?.fullName)} 👋</Text>
        </View>
        <Pressable
          accessibilityLabel="Ir a tu perfil"
          onPress={() => router.navigate('/profile')}
          style={{ width: 44, height: 44, borderRadius: 22, backgroundColor: colors.accentTint, alignItems: 'center', justifyContent: 'center' }}
        >
          <Text variant="heading" style={{ color: colors.warning }}>{firstName(user?.fullName).charAt(0).toUpperCase()}</Text>
        </Pressable>
      </View>

      {hasError ? (
        <ErrorState message={overview.error?.message} onRetry={refresh} />
      ) : (
        <>
          <HeroCard
            loading={overview.isLoading}
            newTotal={overview.data?.newTotal ?? 0}
            newStrong={overview.data?.newStrong ?? 0}
            newPossible={overview.data?.newPossible ?? 0}
            newNotRecommended={overview.data?.newNotRecommended ?? 0}
            onSeeAll={() => router.navigate('/jobs')}
          />

          <View style={{ flexDirection: 'row', gap: 10 }}>
            <StatTile loading={overview.isLoading} value={overview.data?.strong} label="Muy compatibles" color={colors.success} bg={colors.successTint} icon="star" />
            <StatTile loading={overview.isLoading} value={overview.data?.possible} label="Posibles" color={colors.info} bg={colors.infoTint} icon="checkmark-circle" />
            <StatTile loading={overview.isLoading} value={overview.data?.review} label="Revisar" color={colors.warning} bg={colors.warningTint} icon="eye" />
          </View>

          {needsProfile ? (
            <Card tone="muted" style={{ gap: 10, borderRadius: radius.lg }}>
              <Text variant="heading">Cuéntale a tu agente quién eres</Text>
              <Text tone="muted">Agrega tus habilidades y tus preferencias para que pueda calcular qué ofertas encajan contigo.</Text>
              <Button label="Completar mi perfil" icon="create-outline" onPress={() => router.push('/edit-profile')} />
            </Card>
          ) : null}

          <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
            <Text variant="title">Oportunidades para ti</Text>
            {items.length > 0 ? (
              <Pressable onPress={() => router.navigate('/jobs')} hitSlop={8}>
                <Text variant="caption" tone="primary" style={{ fontFamily: 'PlusJakartaSans_700Bold' }}>Ver todas</Text>
              </Pressable>
            ) : null}
          </View>

          {feed.isLoading ? (
            <JobListSkeleton />
          ) : feed.isError ? (
            <ErrorState message={feed.error.message} onRetry={() => void feed.refetch()} />
          ) : items.length === 0 ? (
            <EmptyState
              icon="radio-outline"
              title="No encontramos nuevas oportunidades todavía"
              message="Tu agente seguirá buscando y te avisará cuando aparezca algo que encaje contigo."
            />
          ) : (
            <View style={{ gap: 12 }}>
              {items.map((item) => (
                <JobCard
                  key={item.job.id}
                  item={item}
                  busy={busy}
                  onOpen={() => open(item.job.id)}
                  onInterested={() => save(item.job.id)}
                  onDismiss={() => discard(item.job.id)}
                />
              ))}
            </View>
          )}
        </>
      )}
    </Screen>
  );
}

type HeroProps = {
  loading: boolean;
  newTotal: number;
  newStrong: number;
  newPossible: number;
  newNotRecommended: number;
  onSeeAll: () => void;
};

function HeroCard({ loading, newTotal, newStrong, newPossible, newNotRecommended, onSeeAll }: HeroProps) {
  const { colors } = useTheme();

  if (loading) {
    return (
      <Card tone="brand" style={{ gap: 14 }}>
        <Skeleton width="35%" height={14} style={{ backgroundColor: 'rgba(255,255,255,0.35)' }} />
        <Skeleton width="45%" height={44} style={{ backgroundColor: 'rgba(255,255,255,0.35)' }} />
        <Skeleton width="60%" height={14} style={{ backgroundColor: 'rgba(255,255,255,0.35)' }} />
      </Card>
    );
  }

  if (newTotal === 0) {
    return (
      <Card tone="brand" style={{ gap: 10 }} testID="home-hero">
        <Icon name="radio-outline" size={26} tone="onPrimary" />
        <Text variant="title" tone="onPrimary">Tu agente sigue buscando</Text>
        <Text tone="onPrimary" style={{ opacity: 0.9 }}>No hay ofertas nuevas por revisar. Te avisaremos cuando aparezca algo para ti.</Text>
        <Button label="Ver mis oportunidades" variant="secondary" onPress={onSeeAll} />
      </Card>
    );
  }

  return (
    <Card tone="brand" style={{ gap: 12 }} testID="home-hero">
      <Text variant="caption" tone="onPrimary" style={{ opacity: 0.85 }}>Encontramos</Text>
      <View style={{ flexDirection: 'row', alignItems: 'baseline', gap: 10 }}>
        <Text variant="display" tone="onPrimary" style={{ fontSize: 52, lineHeight: 56 }} testID="home-new-total">{newTotal}</Text>
        <Text variant="heading" tone="onPrimary">oportunidades nuevas</Text>
      </View>
      <View style={{ gap: 4 }}>
        <Text tone="onPrimary" style={{ fontFamily: 'PlusJakartaSans_700Bold' }}>
          {newStrong} {newStrong === 1 ? 'encaja' : 'encajan'} muy bien contigo
        </Text>
        <Text variant="caption" tone="onPrimary" style={{ opacity: 0.85 }}>
          {newPossible} con requisitos que podrías cumplir · {newNotRecommended} no te recomendamos
        </Text>
      </View>
      <Button label="Ver oportunidades" icon="arrow-forward" variant="secondary" onPress={onSeeAll} testID="home-see-jobs" style={{ marginTop: 4, backgroundColor: colors.surface }} />
    </Card>
  );
}

function StatTile({ loading, value, label, color, bg, icon }: { loading: boolean; value: number | undefined; label: string; color: string; bg: string; icon: 'star' | 'checkmark-circle' | 'eye' }) {
  return (
    <Card style={{ flex: 1, gap: 8, padding: 12 }}>
      <View style={{ width: 30, height: 30, borderRadius: 15, backgroundColor: bg, alignItems: 'center', justifyContent: 'center' }}>
        <Icon name={icon} size={16} color={color} />
      </View>
      {loading ? <Skeleton width={28} height={26} /> : <Text variant="title" style={{ color }}>{value ?? 0}</Text>}
      <Text variant="caption" tone="muted" numberOfLines={1}>{label}</Text>
    </Card>
  );
}
