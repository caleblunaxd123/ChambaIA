import { useRouter } from 'expo-router';
import { Pressable, View } from 'react-native';

import type { FeedFilters, FeedTab } from '@/api/endpoints';
import { useApplications, useFeed, useNotificationSummary, useOverview, usePreferences, useProfile, useResumes } from '@/api/queries';
import type { ApplicationStatus, JobApplication, Overview } from '@/api/schemas';
import { JobCard } from '@/features/jobs/JobCard';
import { jobsLink } from '@/features/jobs/links';
import { useJobActions } from '@/features/jobs/useJobActions';
import { firstName, formatDateTime, formatRelativeTime, greeting, nextInterview, plural } from '@/lib/format';
import { unreadBadge } from '@/features/notifications/push';
import { profileStrength } from '@/lib/profile-strength';
import { useAuthStore } from '@/state/auth-store';
import { Avatar } from '@/ui/Avatar';
import { Button } from '@/ui/Button';
import { Card } from '@/ui/Card';
import { EmptyState, ErrorState } from '@/ui/EmptyState';
import { Icon, type IconName } from '@/ui/Icon';
import { ProgressBar } from '@/ui/ProgressBar';
import { Screen } from '@/ui/Screen';
import { SectionHeader } from '@/ui/SectionHeader';
import { JobListSkeleton, Skeleton } from '@/ui/Skeleton';
import { Text } from '@/ui/Text';
import { fontFamily, useTheme } from '@/ui/theme';

export default function HomeScreen() {
  const router = useRouter();
  const user = useAuthStore((s) => s.user);
  const overview = useOverview();
  const profile = useProfile();
  const prefs = usePreferences();
  const resumes = useResumes();
  const applications = useApplications();
  const feed = useFeed('forYou', {});
  const unread = useNotificationSummary().data?.unread ?? 0;
  const { open, save, unsave, discard, busy } = useJobActions();

  const refreshing = overview.isRefetching || feed.isRefetching;
  const refresh = () => {
    void overview.refetch();
    void feed.refetch();
    void applications.refetch();
  };

  const goJobs = (tab: FeedTab, filters: FeedFilters = {}) => router.navigate(jobsLink(tab, filters.category));

  const items = feed.data?.pages.flatMap((p) => p.items).slice(0, 3) ?? [];
  const strength = profileStrength(profile.data, prefs.data, (resumes.data?.length ?? 0) > 0);
  const strengthLoaded = profile.data !== undefined && prefs.data !== undefined && resumes.data !== undefined;
  const hasError = overview.isError && feed.isError;
  const name = firstName(user?.fullName);

  return (
    <Screen onRefresh={refresh} refreshing={refreshing}>
      <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
        <View style={{ gap: 2, flex: 1 }}>
          <Text variant="caption" tone="muted">{greeting()}{name ? ',' : ''}</Text>
          <Text variant="display" testID="home-greeting" numberOfLines={1}>{name || 'Hola'} 👋</Text>
        </View>
        <NotificationBell unread={unread} onPress={() => router.push('/notifications')} />
        <Pressable accessibilityRole="button" accessibilityLabel="Ir a tu perfil" onPress={() => router.navigate('/profile')} hitSlop={6}>
          <Avatar name={user?.fullName ?? '?'} size={46} shape="circle" />
        </Pressable>
      </View>

      {hasError ? (
        <ErrorState message={overview.error?.message} onRetry={refresh} />
      ) : (
        <>
          <HeroCard overview={overview.data} loading={overview.isLoading} onReviewNew={() => goJobs('new')} onSeeAll={() => goJobs('forYou')} />

          <View style={{ flexDirection: 'row', gap: 10 }}>
            <StatTile loading={overview.isLoading} value={overview.data?.strong} label="Muy compatibles" tone="success" icon="star" onPress={() => goJobs('forYou')} testID="stat-strong" />
            <StatTile loading={overview.isLoading} value={overview.data?.possible} label="Compatibles" tone="info" icon="checkmark-circle" onPress={() => goJobs('forYou', { category: 'compatible' })} testID="stat-possible" />
            <StatTile loading={overview.isLoading} value={overview.data?.review} label="Para revisar" tone="warning" icon="eye" onPress={() => goJobs('forYou', { category: 'review' })} testID="stat-review" />
          </View>

          {strengthLoaded && strength.next ? (
            <StrengthCard percent={strength.percent} value={strength.value} label={strength.label} next={strength.next.label} onPress={() => router.push(strength.next?.key === 'cv' ? { pathname: '/onboarding', params: { mode: 'cv' } } : (strength.next?.route ?? '/edit-profile'))} />
          ) : null}

          <PipelineCard applications={applications.data ?? []} onPress={() => router.navigate('/applications')} />

          <SectionHeader title="Lo mejor para ti" subtitle="Ordenado por qué tan bien encaja contigo" action={items.length > 0 ? 'Ver todas' : undefined} onAction={() => goJobs('forYou')} testID="home-see-all" />

          {feed.isLoading ? (
            <JobListSkeleton count={2} />
          ) : feed.isError ? (
            <ErrorState message={feed.error.message} onRetry={() => void feed.refetch()} />
          ) : items.length === 0 ? (
            <EmptyState
              icon="radio-outline"
              title="Tu agente sigue buscando"
              message="Aún no hay ofertas que encajen contigo. Completa tu perfil o cuéntale a tu agente qué buscas."
              actionLabel="Hablar con mi agente"
              onAction={() => router.navigate('/agent')}
            />
          ) : (
            <View style={{ gap: 12 }}>
              {items.map((item) => (
                <JobCard
                  key={item.job.id}
                  item={item}
                  busy={busy}
                  onOpen={() => open(item.job.id)}
                  onSave={() => save(item.job.id)}
                  onUnsave={() => unsave(item.job.id)}
                  onDismiss={() => discard(item.job.id, item.match?.status)}
                />
              ))}
            </View>
          )}

          <AgentPrompt onPress={() => router.navigate('/agent')} />
        </>
      )}
    </Screen>
  );
}

/** Bell with the unread count; the only way into the notice inbox. */
function NotificationBell({ unread, onPress }: { unread: number; onPress: () => void }) {
  const { colors } = useTheme();
  const badge = unreadBadge(unread);
  return (
    <Pressable accessibilityRole="button" accessibilityLabel={unread > 0 ? `Avisos, ${unread} sin leer` : 'Avisos'} onPress={onPress} hitSlop={6} testID="home-bell" style={{ width: 44, height: 44, borderRadius: 22, backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border, alignItems: 'center', justifyContent: 'center' }}>
      <Icon name={unread > 0 ? 'notifications' : 'notifications-outline'} size={22} tone={unread > 0 ? 'primary' : 'muted'} />
      {badge ? (
        <View testID="home-bell-badge" style={{ position: 'absolute', top: -2, right: -2, minWidth: 18, height: 18, paddingHorizontal: 4, borderRadius: 9, backgroundColor: colors.accent, alignItems: 'center', justifyContent: 'center' }}>
          <Text variant="caption" style={{ color: '#fff', fontSize: 11, lineHeight: 14, fontFamily: fontFamily.bold }}>{badge}</Text>
        </View>
      ) : null}
    </Pressable>
  );
}

function HeroCard({ overview, loading, onReviewNew, onSeeAll }: { overview: Overview | undefined; loading: boolean; onReviewNew: () => void; onSeeAll: () => void }) {
  const { colors } = useTheme();

  if (loading || !overview) {
    return (
      <Card tone="brand" style={{ gap: 14 }}>
        <Skeleton onHero width="45%" height={14} />
        <Skeleton onHero width="40%" height={48} />
        <Skeleton onHero width="100%" height={10} />
        <Skeleton onHero width="55%" height={44} radius={22} />
      </Card>
    );
  }

  const { newTotal, newStrong, newPossible, newNotRecommended, lastUpdatedAt } = overview;

  return (
    <Card tone="brand" style={{ gap: 16 }} testID="home-hero">
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
        <View style={{ width: 8, height: 8, borderRadius: 4, backgroundColor: colors.live }} />
        <Text variant="caption" tone="onHero" style={{ opacity: 0.9 }}>
          Tu agente está activo{lastUpdatedAt ? ` · revisó ${formatRelativeTime(lastUpdatedAt)}` : ''}
        </Text>
      </View>

      {newTotal === 0 ? (
        <View style={{ gap: 6 }}>
          <Text variant="title" tone="onHero">Estás al día</Text>
          <Text tone="onHero" style={{ opacity: 0.88 }}>No hay ofertas nuevas por revisar. Te avisaremos cuando aparezca algo para ti.</Text>
        </View>
      ) : (
        <>
          <View style={{ gap: 2 }}>
            <View style={{ flexDirection: 'row', alignItems: 'baseline', gap: 10 }}>
              <Text variant="display" tone="onHero" style={{ fontSize: 52, lineHeight: 58 }} testID="home-new-total">{newTotal}</Text>
              <Text variant="heading" tone="onHero" style={{ flexShrink: 1 }}>{newTotal === 1 ? 'oportunidad nueva' : 'oportunidades nuevas'}</Text>
            </View>
            <Text tone="onHero" style={{ fontFamily: fontFamily.semibold }}>
              {newStrong > 0 ? `${newStrong} ${newStrong === 1 ? 'encaja' : 'encajan'} muy bien contigo` : 'Revisa las que podrían encajar contigo'}
            </Text>
          </View>

          <View style={{ gap: 10 }}>
            <ProgressBar
              height={10}
              track={colors.onHeroTint}
              segments={[
                { value: newStrong, color: colors.live },
                { value: newPossible, color: colors.onHeroAccent },
                { value: newNotRecommended, color: colors.onHeroTint },
              ]}
            />
            <View style={{ flexDirection: 'row', flexWrap: 'wrap', columnGap: 14, rowGap: 4 }}>
              {newStrong > 0 ? <Legend color={colors.live} text={plural(newStrong, 'muy compatible', 'muy compatibles')} /> : null}
              {newPossible > 0 ? <Legend color={colors.onHeroAccent} text={plural(newPossible, 'posible', 'posibles')} /> : null}
              {newNotRecommended > 0 ? <Legend color={colors.onHeroTint} text={`${newNotRecommended} no te ${newNotRecommended === 1 ? 'conviene' : 'convienen'}`} /> : null}
            </View>
          </View>
        </>
      )}

      <Button
        label={newTotal === 0 ? 'Ver mis oportunidades' : 'Revisar las nuevas'}
        trailingIcon="arrow-forward"
        variant="onHero"
        onPress={newTotal === 0 ? onSeeAll : onReviewNew}
        testID="home-see-jobs"
      />
    </Card>
  );
}

function Legend({ color, text }: { color: string; text: string }) {
  const { colors } = useTheme();
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
      <View style={{ width: 8, height: 8, borderRadius: 4, backgroundColor: color, borderWidth: 1, borderColor: colors.onHeroMuted }} />
      <Text variant="caption" tone="onHero" style={{ opacity: 0.9 }}>{text}</Text>
    </View>
  );
}

type StatTone = 'success' | 'info' | 'warning';

function StatTile({ loading, value, label, tone, icon, onPress, testID }: { loading: boolean; value: number | undefined; label: string; tone: StatTone; icon: IconName; onPress: () => void; testID?: string }) {
  const { colors } = useTheme();
  const tint = { success: colors.successTint, info: colors.infoTint, warning: colors.warningTint }[tone];
  return (
    <Card onPress={onPress} testID={testID} accessibilityLabel={`${value ?? 0} ${label}. Ver ofertas`} style={{ flex: 1, gap: 8, padding: 12 }}>
      <View style={{ width: 30, height: 30, borderRadius: 10, backgroundColor: tint, alignItems: 'center', justifyContent: 'center' }}>
        <Icon name={icon} size={16} tone={tone} />
      </View>
      {loading ? <Skeleton width={28} height={26} /> : <Text variant="title" tone={tone}>{value ?? 0}</Text>}
      <Text variant="caption" tone="muted" numberOfLines={2} style={{ fontSize: 12, lineHeight: 16 }}>{label}</Text>
    </Card>
  );
}

function StrengthCard({ percent, value, label, next, onPress }: { percent: number; value: number; label: string; next: string; onPress: () => void }) {
  const { colors } = useTheme();
  return (
    <Card onPress={onPress} testID="strength-card" accessibilityLabel={`Tu perfil está al ${percent} por ciento. Siguiente paso: ${next}`} style={{ gap: 12 }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 12 }}>
        <View style={{ width: 44, height: 44, borderRadius: 14, backgroundColor: colors.accentTint, alignItems: 'center', justifyContent: 'center' }}>
          <Icon name="rocket-outline" size={22} tone="accent" />
        </View>
        <View style={{ flex: 1, gap: 2 }}>
          <Text variant="bodyStrong">Mejora tus resultados</Text>
          <Text variant="caption" tone="muted">{label} · {percent}% de tu perfil</Text>
        </View>
        <Icon name="chevron-forward" size={18} tone="subtle" />
      </View>
      <ProgressBar value={value} color={colors.accent} />
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
        <Icon name="arrow-forward-circle" size={16} tone="primary" />
        <Text variant="caption" tone="primary" style={{ fontFamily: fontFamily.bold }}>Siguiente paso: {next}</Text>
      </View>
    </Card>
  );
}

function PipelineCard({ applications, onPress }: { applications: JobApplication[]; onPress: () => void }) {
  const { colors } = useTheme();
  const active = applications.filter((a) => a.status === 'applied' || a.status === 'interview' || a.status === 'offer');
  if (active.length === 0) return null;

  const next = nextInterview(applications);
  const count = (s: ApplicationStatus) => applications.filter((a) => a.status === s).length;

  return (
    <Card onPress={onPress} testID="pipeline-card" style={{ gap: 12 }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
        <Text variant="heading">Tus postulaciones</Text>
        <Icon name="chevron-forward" size={18} tone="subtle" />
      </View>
      <View style={{ flexDirection: 'row', gap: 8 }}>
        <Mini value={count('applied')} label="Postuladas" color={colors.info} />
        <Mini value={count('interview')} label="Entrevistas" color={colors.accent} />
        <Mini value={count('offer')} label="Ofertas" color={colors.success} />
      </View>
      {next ? (
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 10, padding: 10, borderRadius: 12, backgroundColor: colors.accentTint }}>
          <Icon name="calendar" size={18} tone="accent" />
          <Text variant="caption" style={{ flex: 1 }} numberOfLines={2}>
            Próxima entrevista: <Text variant="caption" style={{ fontFamily: fontFamily.bold }}>{formatDateTime(next.interviewDate)}</Text>
            {next.job ? ` · ${next.job.title}` : ''}
          </Text>
        </View>
      ) : null}
    </Card>
  );
}

function Mini({ value, label, color }: { value: number; label: string; color: string }) {
  const { colors } = useTheme();
  return (
    <View style={{ flex: 1, paddingVertical: 10, paddingHorizontal: 12, borderRadius: 12, backgroundColor: colors.bg, gap: 2 }}>
      <Text variant="title" style={{ color }}>{value}</Text>
      <Text variant="caption" tone="muted" numberOfLines={1}>{label}</Text>
    </View>
  );
}

function AgentPrompt({ onPress }: { onPress: () => void }) {
  const { colors } = useTheme();
  return (
    <Card tone="muted" onPress={onPress} testID="agent-prompt" style={{ flexDirection: 'row', alignItems: 'center', gap: 12 }}>
      <View style={{ width: 44, height: 44, borderRadius: 22, backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center' }}>
        <Icon name="chatbubble-ellipses" size={22} tone="onPrimary" />
      </View>
      <View style={{ flex: 1, gap: 2 }}>
        <Text variant="bodyStrong">¿Algo que no quieres ver?</Text>
        <Text variant="caption" tone="muted">Dile a tu agente «no quiero call center» o «mínimo 2000 soles».</Text>
      </View>
      <Icon name="chevron-forward" size={18} tone="subtle" />
    </Card>
  );
}
