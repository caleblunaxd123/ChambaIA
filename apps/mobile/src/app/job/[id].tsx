import { useLocalSearchParams, useRouter } from 'expo-router';
import * as WebBrowser from 'expo-web-browser';
import { useEffect, useRef, useState } from 'react';
import { Pressable, ScrollView, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import type { JobDetailResponse, MatchNote } from '@/api/schemas';
import { useCreateApplication, useDismissMutation, useInterestedMutation, useJobDetail, useSeenMutation } from '@/api/queries';
import { MatchBadge } from '@/features/jobs/MatchBadge';
import { SkillBadge } from '@/features/jobs/SkillBadge';
import { educationLabel, formatDuration, formatRelativeTime, formatSalary, modalityLabel, skillLevelLabel } from '@/lib/format';
import { haptics } from '@/lib/haptics';
import { Badge } from '@/ui/Badge';
import { Button } from '@/ui/Button';
import { Card } from '@/ui/Card';
import { ConfirmDialog } from '@/ui/ConfirmDialog';
import { ErrorState } from '@/ui/EmptyState';
import { Icon } from '@/ui/Icon';
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
  const interested = useInterestedMutation();
  const dismiss = useDismissMutation();
  const createApplication = useCreateApplication();
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

  const apply = async (url: string) => {
    haptics.tap();
    await WebBrowser.openBrowserAsync(url).catch(() => undefined);
    setAskApplied(true);
  };

  return (
    <View style={{ flex: 1, backgroundColor: colors.bg }}>
      <View style={{ paddingTop: insets.top + 8, paddingHorizontal: spacing.lg, paddingBottom: 8, flexDirection: 'row', alignItems: 'center', gap: 12, maxWidth: SCREEN_MAX_WIDTH, width: '100%', alignSelf: 'center' }}>
        <Pressable onPress={back} accessibilityLabel="Volver" hitSlop={12} testID="job-back" style={{ width: 40, height: 40, borderRadius: 20, backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border, alignItems: 'center', justifyContent: 'center' }}>
          <Icon name="chevron-back" size={22} />
        </Pressable>
        <Text variant="heading">Detalle de la oferta</Text>
      </View>

      {detail.isLoading ? (
        <DetailSkeleton />
      ) : detail.isError || !detail.data ? (
        <ErrorState message={detail.error?.message} onRetry={() => void detail.refetch()} />
      ) : (
        <>
          <ScrollView contentContainerStyle={{ width: '100%', maxWidth: SCREEN_MAX_WIDTH, alignSelf: 'center', padding: spacing.lg, paddingBottom: 140, gap: spacing.lg }} showsVerticalScrollIndicator={false}>
            <DetailBody data={detail.data} />
          </ScrollView>

          <View style={{ position: 'absolute', left: 0, right: 0, bottom: 0, backgroundColor: colors.surface, borderTopWidth: 1, borderTopColor: colors.border, paddingBottom: insets.bottom + 12, paddingTop: 12 }}>
            <View style={{ maxWidth: SCREEN_MAX_WIDTH, width: '100%', alignSelf: 'center', paddingHorizontal: spacing.lg, gap: 10 }}>
              <Button label="Postular en la página oficial" icon="open-outline" onPress={() => void apply(detail.data.job.originalUrl)} fullWidth testID="job-apply" />
              <View style={{ flexDirection: 'row', gap: 10 }}>
                <Button
                  label={detail.data.match?.status === 'interested' || detail.data.application ? 'Guardada' : 'Me interesa'}
                  icon="bookmark-outline"
                  variant="secondary"
                  style={{ flex: 1 }}
                  disabled={!detail.data.match || detail.data.match.status === 'interested'}
                  loading={interested.isPending}
                  onPress={() => interested.mutate(id, { onSuccess: () => haptics.success() })}
                  testID="job-interested"
                />
                <Button
                  label="Descartar"
                  variant="ghost"
                  style={{ flex: 1 }}
                  disabled={!detail.data.match}
                  loading={dismiss.isPending}
                  onPress={() => dismiss.mutate(id, { onSuccess: () => { haptics.warning(); back(); } })}
                  testID="job-dismiss"
                />
              </View>
            </View>
          </View>
        </>
      )}

      <ConfirmDialog
        visible={askApplied}
        title="¿Ya postulaste?"
        message="Si enviaste tu postulación, la guardamos en tu tablero para que le hagas seguimiento."
        confirmLabel="Sí, ya postulé"
        cancelLabel="Todavía no"
        onCancel={() => setAskApplied(false)}
        onConfirm={() => {
          setAskApplied(false);
          createApplication.mutate({ jobId: id, status: 'applied' }, { onSuccess: () => haptics.success() });
        }}
      />
    </View>
  );
}

function DetailBody({ data }: { data: JobDetailResponse }) {
  const { job, match, application } = data;
  const s = job.summary;
  const salary = formatSalary(s);

  return (
    <>
      <View style={{ gap: 10 }}>
        {match ? <MatchBadge category={match.category} /> : null}
        <Text variant="display" testID="job-title">{s.title}</Text>
        <Text variant="heading" tone="muted">{s.company}</Text>
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
          <Badge label={s.district ?? s.city} icon="location-outline" />
          <Badge label={modalityLabel[s.modality]} icon="business-outline" />
          <Badge label={salary ?? 'Sueldo no indicado'} icon="cash-outline" tone={salary ? 'brand' : 'neutral'} />
          {job.schedule ? <Badge label={job.schedule} icon="time-outline" /> : null}
        </View>
        <Text variant="caption" tone="subtle">
          {s.postedAt ? `Publicada ${formatRelativeTime(s.postedAt)} · ` : ''}Fuente: {s.sourceName}
        </Text>
        {application ? <Badge label="Está en tu tablero de postulaciones" tone="success" icon="paper-plane" /> : null}
      </View>

      {match ? <WhyCard match={match} /> : <Card tone="muted"><Text tone="muted">Aún no analizamos esta oferta contra tu perfil. Completa tu perfil para ver qué tan bien encaja.</Text></Card>}

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

      <Card style={{ gap: 8 }}>
        <Text variant="heading">Descripción</Text>
        <Text tone="muted">{job.description}</Text>
      </Card>
    </>
  );
}

function WhyCard({ match }: { match: NonNullable<JobDetailResponse['match']> }) {
  const { colors } = useTheme();
  return (
    <Card style={{ gap: 14 }} testID="why-card">
      <Text variant="heading">¿Por qué esta oferta encaja contigo?</Text>
      <Text tone="muted">{match.recommendation}</Text>

      {match.reasons.length > 0 ? (
        <View style={{ gap: 10 }}>
          <Text variant="label" tone="success">Coincidencias</Text>
          {match.reasons.map((n, i) => <Note key={`${n.code}-${i}`} note={n} kind="ok" />)}
        </View>
      ) : null}

      {match.warnings.length > 0 ? (
        <View style={{ gap: 10, paddingTop: 12, borderTopWidth: 1, borderTopColor: colors.border }}>
          <Text variant="label" tone="warning">A revisar</Text>
          {match.warnings.map((n, i) => <Note key={`${n.code}-${i}`} note={n} kind="warn" />)}
        </View>
      ) : null}
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

function Requirement({ icon, text }: { icon: 'briefcase-outline' | 'school-outline'; text: string }) {
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 10 }}>
      <Icon name={icon} size={18} tone="muted" />
      <Text>{text}</Text>
    </View>
  );
}

function DetailSkeleton() {
  return (
    <View style={{ padding: 16, gap: 14 }}>
      <Skeleton width={120} height={24} radius={12} />
      <Skeleton width="85%" height={30} />
      <Skeleton width="50%" height={18} />
      <Skeleton height={160} radius={20} />
      <Skeleton height={120} radius={20} />
    </View>
  );
}
