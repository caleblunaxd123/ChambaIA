import { useLocalSearchParams, useRouter } from 'expo-router';
import * as WebBrowser from 'expo-web-browser';
import { useEffect, useRef, useState } from 'react';
import { Platform, Pressable, ScrollView, Share, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import type { JobDetailResponse, MatchNote } from '@/api/schemas';
import { useCreateApplication, useJobDetail, useSeenMutation } from '@/api/queries';
import { PrepCard } from '@/features/assistant/ApplicationPrep';
import { FitBreakdown, ImprovementsCard, SimilarJobs } from '@/features/jobs/Explanation';
import { categoryStyle } from '@/features/jobs/MatchBadge';
import { SkillBadge } from '@/features/jobs/SkillBadge';
import { useJobActions } from '@/features/jobs/useJobActions';
import { applicationStatusLabel, educationLabel, formatDuration, formatRelativeTime, formatSalary, modalityLabel, skillLevelLabel } from '@/lib/format';
import { haptics } from '@/lib/haptics';
import { toast } from '@/state/toast-store';
import { Avatar } from '@/ui/Avatar';
import { Badge, useBadgeColors } from '@/ui/Badge';
import { Button, IconButton } from '@/ui/Button';
import { Card } from '@/ui/Card';
import { ConfirmDialog } from '@/ui/ConfirmDialog';
import { ErrorState } from '@/ui/EmptyState';
import { Icon, type IconName } from '@/ui/Icon';
import { SCREEN_MAX_WIDTH } from '@/ui/Screen';
import { Skeleton } from '@/ui/Skeleton';
import { Text } from '@/ui/Text';
import { useTheme } from '@/ui/theme';

export default function JobDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { colors, spacing } = useTheme();
  const insets = useSafeAreaInsets();
  const detail = useJobDetail(id);
  const seen = useSeenMutation();
  const createApplication = useCreateApplication();
  const { save, unsave, discard, busy } = useJobActions();
  const [askApplied, setAskApplied] = useState(false);
  const markedSeen = useRef(false);

  const status = detail.data?.match?.status;
  useEffect(() => {
    if (status === 'new' && !markedSeen.current) {
      markedSeen.current = true;
      seen.mutate(id);
    }
  }, [status, id, seen]);

  const back = () => (router.canGoBack() ? router.back() : router.replace('/jobs'));

  const data = detail.data;
  const application = data?.application;
  const alreadyApplied = application != null && (application.status === 'applied' || application.status === 'interview' || application.status === 'offer');
  const saved = status === 'interested' || application?.status === 'interested';

  const apply = async (url: string) => {
    haptics.tap();
    await WebBrowser.openBrowserAsync(url).catch(() => undefined);
    if (!alreadyApplied) setAskApplied(true);
  };

  const share = async () => {
    if (!data) return;
    const s = data.job.summary;
    const message = `${s.title} en ${s.company}${formatSalary(s) ? ` · ${formatSalary(s)}` : ''}\n${data.job.originalUrl}`;
    try {
      if (Platform.OS === 'web' && typeof navigator !== 'undefined' && navigator.clipboard) {
        await navigator.clipboard.writeText(message);
        toast.show({ message: 'Enlace copiado', tone: 'success' });
      } else {
        await Share.share({ message });
      }
    } catch {
      // The user closed the share sheet: nothing to do.
    }
  };

  return (
    <View style={{ flex: 1, backgroundColor: colors.bg }}>
      <View style={{ paddingTop: insets.top + 8, paddingHorizontal: spacing.lg, paddingBottom: 8, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 12, maxWidth: SCREEN_MAX_WIDTH, width: '100%', alignSelf: 'center' }}>
        <IconButton icon="chevron-back" label="Volver" onPress={back} testID="job-back" />
        <Text variant="bodyStrong" tone="muted" numberOfLines={1} style={{ flex: 1, textAlign: 'center' }}>{data?.job.summary.company ?? 'Oferta'}</Text>
        <IconButton icon="share-outline" label="Compartir oferta" onPress={() => void share()} disabled={!data} testID="job-share" />
      </View>

      {detail.isLoading ? (
        <DetailSkeleton />
      ) : detail.isError || !data ? (
        <ErrorState message={detail.error?.message} onRetry={() => void detail.refetch()} />
      ) : (
        <>
          <ScrollView contentContainerStyle={{ width: '100%', maxWidth: SCREEN_MAX_WIDTH, alignSelf: 'center', padding: spacing.lg, paddingBottom: 120 + insets.bottom, gap: spacing.lg }} showsVerticalScrollIndicator={false}>
            <DetailBody data={data} onOpenTracker={() => router.navigate('/applications')} onEditProfile={() => router.push('/edit-profile')} />

            {data.match && !alreadyApplied && data.match.status !== 'dismissed' ? (
              <Card tone="muted" style={{ flexDirection: 'row', alignItems: 'center', gap: 12 }}>
                <View style={{ flex: 1, gap: 2 }}>
                  <Text variant="bodyStrong">¿No te convence?</Text>
                  <Text variant="caption" tone="muted">Descártala y tu agente no te la volverá a mostrar.</Text>
                </View>
                <Button label="Descartar" icon="close" variant="secondary" size="sm" disabled={busy} onPress={() => discard(id, status, back)} testID="job-dismiss" />
              </Card>
            ) : null}
          </ScrollView>

          <View style={{ position: 'absolute', left: 0, right: 0, bottom: 0, backgroundColor: colors.surface, borderTopWidth: 1, borderTopColor: colors.border, paddingBottom: insets.bottom + 12, paddingTop: 12 }}>
            <View style={{ maxWidth: SCREEN_MAX_WIDTH, width: '100%', alignSelf: 'center', paddingHorizontal: spacing.lg, flexDirection: 'row', alignItems: 'center', gap: 10 }}>
              {data.match && !alreadyApplied ? (
                <IconButton
                  icon={saved ? 'bookmark' : 'bookmark-outline'}
                  label={saved ? 'Quitar de guardadas' : 'Guardar oferta'}
                  size={52}
                  variant="tonal"
                  selected={saved}
                  disabled={busy}
                  onPress={() => (saved ? unsave(id) : save(id))}
                  testID="job-interested"
                />
              ) : null}
              <Button
                label={alreadyApplied ? 'Ver la oferta original' : 'Postular en la página oficial'}
                icon="open-outline"
                variant={alreadyApplied ? 'secondary' : 'primary'}
                onPress={() => void apply(data.job.originalUrl)}
                style={{ flex: 1 }}
                fullWidth
                testID="job-apply"
              />
            </View>
          </View>
        </>
      )}

      <ConfirmDialog
        visible={askApplied}
        icon="paper-plane-outline"
        title="¿Ya postulaste?"
        message="Si enviaste tu postulación, la guardamos en tu tablero para que le hagas seguimiento."
        confirmLabel="Sí, ya postulé"
        cancelLabel="Todavía no"
        onCancel={() => setAskApplied(false)}
        onConfirm={() => {
          setAskApplied(false);
          createApplication.mutate(
            { jobId: id, status: 'applied' },
            {
              onSuccess: () => {
                haptics.success();
                toast.show({ message: '¡Bien hecho! La agregamos a tus postulaciones.', tone: 'success', action: { label: 'Ver', onPress: () => router.navigate('/applications') } });
              },
              onError: (error) => toast.show({ message: error.message, tone: 'danger' }),
            },
          );
        }}
      />
    </View>
  );
}

function DetailBody({ data, onOpenTracker, onEditProfile }: { data: JobDetailResponse; onOpenTracker: () => void; onEditProfile: () => void }) {
  const { job, match, application } = data;
  const s = job.summary;
  const salary = formatSalary(s);

  return (
    <>
      <View style={{ gap: 14 }}>
        <View style={{ flexDirection: 'row', gap: 14, alignItems: 'center' }}>
          <Avatar name={s.company} size={60} />
          <View style={{ flex: 1, gap: 2 }}>
            <Text variant="title" testID="job-title">{s.title}</Text>
            <Text tone="muted">{s.company}</Text>
          </View>
        </View>
        <Text variant="caption" tone="subtle">
          {s.postedAt ? `Publicada ${formatRelativeTime(s.postedAt)} · ` : ''}Fuente: {s.sourceName}
        </Text>
        {application ? (
          <Pressable onPress={onOpenTracker} accessibilityRole="button" accessibilityLabel="Ver en mis postulaciones">
            <Badge label={`En tus postulaciones · ${applicationStatusLabel[application.status]}`} tone="info" icon="paper-plane" />
          </Pressable>
        ) : null}
      </View>

      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 10 }}>
        <Fact icon="cash-outline" label="Sueldo" value={salary ?? 'No indicado'} highlight={salary !== null} />
        <Fact icon="location-outline" label="Ubicación" value={s.district ?? s.city} />
        <Fact icon="business-outline" label="Modalidad" value={modalityLabel[s.modality]} />
        <Fact icon="time-outline" label="Horario" value={job.schedule ?? (job.weekdaysOnly ? 'Lunes a viernes' : 'No indicado')} />
      </View>

      {match ? (
        <WhyCard match={match} />
      ) : (
        <Card tone="muted">
          <Text tone="muted">Aún no analizamos esta oferta contra tu perfil. Completa tu perfil para ver qué tan bien encaja.</Text>
        </Card>
      )}

      {match ? <ImprovementsCard improvements={match.improvements} onEditProfile={onEditProfile} /> : null}

      <Card style={{ gap: 12 }}>
        <Text variant="heading">Lo que piden</Text>
        {job.experienceRequiredMinMonths != null ? <Requirement icon="briefcase-outline" text={job.experienceRequiredMinMonths === 0 ? 'No exigen experiencia previa' : `Experiencia: ${formatDuration(job.experienceRequiredMinMonths)}`} /> : null}
        {job.educationRequired ? <Requirement icon="school-outline" text={`Estudios: ${educationLabel[job.educationRequired].toLowerCase()}${job.educationRequiredCompleted ? ' completos' : ''}`} /> : null}
        {job.skillsRequired.length > 0 ? (
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
            {job.skillsRequired.map((k) => (
              <SkillBadge key={k.key} name={k.minLevel ? `${k.name} · ${skillLevelLabel[k.minLevel].toLowerCase()}` : k.name} state={match ? (match.missingSkills.includes(k.name) ? 'missing' : 'matched') : 'neutral'} />
            ))}
          </View>
        ) : null}
        {job.skillsPreferred.length > 0 ? (
          <View style={{ gap: 6 }}>
            <Text variant="caption" tone="muted">Valoran además</Text>
            <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
              {job.skillsPreferred.map((k) => <SkillBadge key={k.key} name={k.name} />)}
            </View>
          </View>
        ) : null}
      </Card>

      <Description text={job.description} />

      <PrepCard jobId={s.id} />

      <SimilarJobs jobId={s.id} />
    </>
  );
}

function Fact({ icon, label, value, highlight }: { icon: IconName; label: string; value: string; highlight?: boolean }) {
  const { colors, radius } = useTheme();
  return (
    <View style={{ flexGrow: 1, flexBasis: '45%', padding: 12, gap: 6, borderRadius: radius.md, backgroundColor: highlight ? colors.primaryTint : colors.surface, borderWidth: highlight ? 0 : 1, borderColor: colors.border }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
        <Icon name={icon} size={15} tone={highlight ? 'primary' : 'subtle'} />
        <Text variant="label" tone={highlight ? 'primary' : 'subtle'}>{label}</Text>
      </View>
      <Text variant="bodyStrong" numberOfLines={2}>{value}</Text>
    </View>
  );
}

function WhyCard({ match }: { match: NonNullable<JobDetailResponse['match']> }) {
  const { colors, radius } = useTheme();
  const style = categoryStyle[match.category];
  const { bg, fg } = useBadgeColors(style.tone);

  return (
    <Card padded={false} style={{ overflow: 'hidden' }} testID="why-card">
      <View style={{ backgroundColor: bg, padding: 16, gap: 8 }}>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
          <View style={{ width: 32, height: 32, borderRadius: 16, backgroundColor: colors.surface, alignItems: 'center', justifyContent: 'center' }}>
            <Icon name={style.icon} size={18} color={fg} />
          </View>
          <Text variant="heading" style={{ color: fg }}>{style.label}</Text>
        </View>
        <Text>{match.recommendation}</Text>
      </View>

      <View style={{ padding: 16, gap: 16 }}>
        <FitBreakdown dimensions={match.dimensions} />

        {match.reasons.length > 0 ? (
          <View style={{ gap: 10 }}>
            <Text variant="label" tone="success">Por qué encaja</Text>
            {match.reasons.map((n, i) => <Note key={`${n.code}-${i}`} note={n} kind="ok" />)}
          </View>
        ) : null}

        {match.warnings.length > 0 ? (
          <View style={{ gap: 10, padding: 12, borderRadius: radius.md, backgroundColor: colors.warningTint }}>
            <Text variant="label" tone="warning">Ten en cuenta</Text>
            {match.warnings.map((n, i) => <Note key={`${n.code}-${i}`} note={n} kind="warn" />)}
          </View>
        ) : null}
      </View>
    </Card>
  );
}

function Note({ note, kind }: { note: MatchNote; kind: 'ok' | 'warn' }) {
  return (
    <View style={{ flexDirection: 'row', gap: 10, alignItems: 'flex-start' }}>
      <Icon name={kind === 'ok' ? 'checkmark-circle' : 'alert-circle'} size={20} tone={kind === 'ok' ? 'success' : 'warning'} />
      <View style={{ flex: 1 }}>
        <Text variant="bodyStrong">{note.title}</Text>
        {note.detail ? <Text variant="caption" tone="muted">{note.detail}</Text> : null}
      </View>
    </View>
  );
}

function Requirement({ icon, text }: { icon: IconName; text: string }) {
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 10 }}>
      <Icon name={icon} size={18} tone="muted" />
      <Text style={{ flex: 1 }}>{text}</Text>
    </View>
  );
}

const DESCRIPTION_PREVIEW = 280;

function Description({ text }: { text: string }) {
  const [expanded, setExpanded] = useState(false);
  const long = text.length > DESCRIPTION_PREVIEW;
  return (
    <Card style={{ gap: 8 }}>
      <Text variant="heading">Descripción</Text>
      <Text tone="muted">{expanded || !long ? text : `${text.slice(0, DESCRIPTION_PREVIEW).trimEnd()}…`}</Text>
      {long ? <Button label={expanded ? 'Ver menos' : 'Leer todo'} variant="ghost" size="sm" icon={expanded ? 'chevron-up' : 'chevron-down'} onPress={() => setExpanded((e) => !e)} style={{ marginLeft: -12 }} /> : null}
    </Card>
  );
}

function DetailSkeleton() {
  return (
    <View style={{ padding: 16, gap: 14, width: '100%', maxWidth: SCREEN_MAX_WIDTH, alignSelf: 'center' }}>
      <View style={{ flexDirection: 'row', gap: 14, alignItems: 'center' }}>
        <Skeleton width={60} height={60} radius={18} />
        <View style={{ flex: 1, gap: 8 }}>
          <Skeleton width="85%" height={22} />
          <Skeleton width="50%" height={14} />
        </View>
      </View>
      <View style={{ flexDirection: 'row', gap: 10 }}>
        <Skeleton width="48%" height={64} radius={14} />
        <Skeleton width="48%" height={64} radius={14} />
      </View>
      <Skeleton height={180} radius={20} />
      <Skeleton height={120} radius={20} />
    </View>
  );
}
