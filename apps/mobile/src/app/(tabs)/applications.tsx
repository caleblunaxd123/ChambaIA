import { useRouter } from 'expo-router';
import { useMemo, useState } from 'react';
import { Pressable, ScrollView, View } from 'react-native';

import type { ApplicationStatus, JobApplication } from '@/api/schemas';
import { useApplications, usePatchApplication, useRemoveApplication } from '@/api/queries';
import { ApplicationSheet } from '@/features/applications/ApplicationSheet';
import { APPLICATION_STAGES, stageStyle } from '@/features/applications/stages';
import { applicationStatusLabel, formatDateTime, formatRelativeTime, formatSalary, nextApplicationStage, nextStageAction } from '@/lib/format';
import { haptics } from '@/lib/haptics';
import { trackerStats } from '@/lib/tracker-stats';
import { toast } from '@/state/toast-store';
import { Avatar } from '@/ui/Avatar';
import { useBadgeColors } from '@/ui/Badge';
import { Button, IconButton } from '@/ui/Button';
import { Card } from '@/ui/Card';
import { EmptyState, ErrorState } from '@/ui/EmptyState';
import { Icon } from '@/ui/Icon';
import { Screen } from '@/ui/Screen';
import { JobListSkeleton } from '@/ui/Skeleton';
import { Text } from '@/ui/Text';
import { fontFamily, useTheme } from '@/ui/theme';

const emptyCopy: Record<ApplicationStatus, { title: string; message: string }> = {
  found: { title: 'Nada por aquí', message: 'Las ofertas que quieras seguir sin decidir todavía aparecerán en esta etapa.' },
  interested: { title: 'Aún no guardas ofertas', message: 'Toca «Guardar» en una oferta y la verás aquí, lista para postular.' },
  applied: { title: 'Todavía no registras postulaciones', message: 'Después de postular en la página oficial, márcala como postulada para hacerle seguimiento.' },
  interview: { title: 'Sin entrevistas por ahora', message: 'Cuando te citen, mueve la postulación a esta etapa y anota la fecha.' },
  offer: { title: 'Aún no hay ofertas recibidas', message: 'Tu próxima oferta de trabajo aparecerá aquí. ¡Ánimo!' },
  discarded: { title: 'Nada descartado', message: 'Las ofertas que descartes después de revisarlas se guardan aquí.' },
};

export default function ApplicationsScreen() {
  const router = useRouter();
  const apps = useApplications();
  const patch = usePatchApplication();
  const remove = useRemoveApplication();
  const [selectedStage, setSelectedStage] = useState<ApplicationStatus | null>(null);
  const [editing, setEditing] = useState<JobApplication | null>(null);

  const list = useMemo(() => apps.data ?? [], [apps.data]);
  const counts = useMemo(() => {
    const c = Object.fromEntries(APPLICATION_STAGES.map((s) => [s, 0])) as Record<ApplicationStatus, number>;
    for (const a of list) c[a.status] += 1;
    return c;
  }, [list]);

  // Open on the most advanced stage that has cards, so the board greets the user with what matters most.
  const stage = selectedStage ?? (['offer', 'interview', 'applied', 'interested', 'found'] as const).find((s) => counts[s] > 0) ?? 'interested';
  const visible = list.filter((a) => a.status === stage);
  const active = list.filter((a) => a.status !== 'discarded').length;

  const advance = (a: JobApplication) => {
    const next = nextApplicationStage[a.status];
    if (!next) return;
    // Interviews need a date: open the sheet already on that stage instead of moving blindly.
    if (next === 'interview') {
      setEditing({ ...a, status: 'interview' });
      return;
    }
    patch.mutate(
      { id: a.id, status: next },
      {
        onSuccess: () => {
          haptics.success();
          toast.show({
            message: next === 'offer' ? '¡Felicitaciones! 🎉' : `Movida a «${applicationStatusLabel[next]}»`,
            tone: 'success',
            action: { label: 'Deshacer', onPress: () => patch.mutate({ id: a.id, status: a.status }) },
          });
        },
        onError: (error) => toast.show({ message: error.message, tone: 'danger' }),
      },
    );
  };

  return (
    <Screen onRefresh={() => void apps.refetch()} refreshing={apps.isRefetching}>
      <View style={{ gap: 4 }}>
        <Text variant="display">Postulaciones</Text>
        <Text tone="muted">
          {active > 0 ? `${active} ${active === 1 ? 'oferta en seguimiento' : 'ofertas en seguimiento'}. Muévelas de etapa a medida que avanzas.` : 'Sigue cada oferta desde que te interesa hasta que recibes respuesta.'}
        </Text>
      </View>

      <FunnelCard applications={list} />

      <StagePicker counts={counts} value={stage} onChange={setSelectedStage} />

      {apps.isLoading ? (
        <JobListSkeleton count={2} />
      ) : apps.isError ? (
        <ErrorState message={apps.error.message} onRetry={() => void apps.refetch()} />
      ) : visible.length === 0 ? (
        <EmptyState
          icon={stageStyle[stage].icon}
          title={emptyCopy[stage].title}
          message={emptyCopy[stage].message}
          actionLabel={list.length === 0 || stage === 'interested' ? 'Explorar empleos' : undefined}
          onAction={() => router.navigate('/jobs')}
        />
      ) : (
        <View style={{ gap: 12 }}>
          {visible.map((a) => (
            <ApplicationCard key={a.id} application={a} busy={patch.isPending} onEdit={() => setEditing(a)} onAdvance={() => advance(a)} />
          ))}
        </View>
      )}

      <ApplicationSheet
        application={editing}
        saving={patch.isPending}
        onClose={() => setEditing(null)}
        onOpenJob={(jobId) => router.push({ pathname: '/job/[id]', params: { id: jobId } })}
        onSave={(id, changes) =>
          patch.mutate(
            { id, status: changes.status, notes: changes.notes, interviewDate: changes.interviewDate, clearInterviewDate: changes.clearInterviewDate },
            {
              onSuccess: (updated) => {
                haptics.success();
                toast.show({ message: 'Seguimiento actualizado', tone: 'success' });
                setSelectedStage(updated.status);
                setEditing(null);
              },
              onError: () => haptics.error(),
            },
          )
        }
        onRemove={(id) =>
          remove.mutate(id, {
            onSuccess: () => {
              toast.show({ message: 'La quitamos de tu tablero' });
              setEditing(null);
              setSelectedStage(null);
            },
          })
        }
      />
    </Screen>
  );
}

/** Phase 7 metrics: how the search is going, in one glance. Hidden until the first application. */
function FunnelCard({ applications }: { applications: JobApplication[] }) {
  const { colors, radius } = useTheme();
  const stats = trackerStats(applications);
  if (stats.applied === 0) return null;

  const steps = [
    { label: stats.applied === 1 ? 'Postulación' : 'Postulaciones', value: stats.applied, color: colors.info },
    { label: stats.interviews === 1 ? 'Entrevista' : 'Entrevistas', value: stats.interviews, color: colors.accent },
    { label: stats.offers === 1 ? 'Oferta' : 'Ofertas', value: stats.offers, color: colors.success },
  ];
  const rate = stats.responseRate === null ? null : Math.round(stats.responseRate * 100);

  return (
    <Card style={{ gap: 12 }} testID="funnel-card">
      <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
        <Text variant="heading">Tu avance</Text>
        {rate !== null ? <Text variant="caption" tone="muted">Te llamaron en el {rate}%</Text> : null}
      </View>
      <View style={{ flexDirection: 'row', gap: 8 }}>
        {steps.map((step, i) => (
          <View key={step.label} style={{ flex: 1, gap: 6 }}>
            <View style={{ height: 6, borderRadius: radius.pill, backgroundColor: colors.surfaceMuted, overflow: 'hidden' }}>
              <View style={{ width: `${stats.applied === 0 ? 0 : Math.max(step.value > 0 ? 8 : 0, (step.value / stats.applied) * 100)}%`, height: '100%', backgroundColor: step.color }} />
            </View>
            <View style={{ flexDirection: 'row', alignItems: 'baseline', gap: 4 }}>
              <Text variant="title" style={{ color: step.value > 0 ? step.color : colors.textSubtle }}>{step.value}</Text>
              {i < steps.length - 1 ? <Icon name="chevron-forward" size={12} tone="subtle" /> : null}
            </View>
            <Text variant="caption" tone="muted" numberOfLines={1}>{step.label}</Text>
          </View>
        ))}
      </View>
      {rate !== null && stats.applied >= 5 && rate < 15 ? (
        <Text variant="caption" tone="muted">
          Consejo: revisa las ofertas «Excelente opción» primero y pide a tu agente que descarte las que no encajan.
        </Text>
      ) : null}
    </Card>
  );
}

function StagePicker({ counts, value, onChange }: { counts: Record<ApplicationStatus, number>; value: ApplicationStatus; onChange: (s: ApplicationStatus) => void }) {
  const { spacing } = useTheme();
  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false} style={{ flexGrow: 0, marginHorizontal: -spacing.lg }} contentContainerStyle={{ gap: 8, paddingHorizontal: spacing.lg }}>
      {APPLICATION_STAGES.map((s) => (
        <StageTile key={s} stage={s} count={counts[s]} selected={s === value} onPress={() => onChange(s)} />
      ))}
    </ScrollView>
  );
}

function StageTile({ stage, count, selected, onPress }: { stage: ApplicationStatus; count: number; selected: boolean; onPress: () => void }) {
  const { colors, radius } = useTheme();
  const { bg, fg } = useBadgeColors(stageStyle[stage].tone);
  return (
    <Pressable
      accessibilityRole="tab"
      accessibilityState={{ selected }}
      accessibilityLabel={`${applicationStatusLabel[stage]}: ${count}`}
      testID={`tab-${stage}`}
      onPress={() => {
        if (!selected) haptics.tap();
        onPress();
      }}
      style={{
        width: 104,
        padding: 12,
        gap: 8,
        borderRadius: radius.lg,
        backgroundColor: colors.surface,
        borderWidth: 1.5,
        borderColor: selected ? fg : colors.border,
      }}
    >
      <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
        <View style={{ width: 28, height: 28, borderRadius: 9, backgroundColor: bg, alignItems: 'center', justifyContent: 'center' }}>
          <Icon name={stageStyle[stage].icon} size={15} color={fg} />
        </View>
        <Text variant="title" style={{ color: count > 0 ? colors.text : colors.textSubtle }}>{count}</Text>
      </View>
      <Text variant="caption" tone={selected ? 'default' : 'muted'} style={{ fontFamily: selected ? fontFamily.bold : fontFamily.medium }} numberOfLines={1}>
        {applicationStatusLabel[stage]}
      </Text>
    </Pressable>
  );
}

function ApplicationCard({ application: a, busy, onEdit, onAdvance }: { application: JobApplication; busy: boolean; onEdit: () => void; onAdvance: () => void }) {
  const { colors, radius } = useTheme();
  const salary = a.job ? formatSalary(a.job) : null;
  const nextLabel = nextStageAction[a.status];
  const upcoming = a.status === 'interview' && a.interviewDate;

  return (
    <Card padded={false} testID={`application-${a.id}`}>
      <Pressable onPress={onEdit} accessibilityRole="button" accessibilityLabel={`${a.job?.title ?? 'Oferta'}. Editar seguimiento`} style={({ pressed }) => ({ padding: 16, gap: 12, borderRadius: radius.lg, backgroundColor: pressed ? colors.surfaceMuted : 'transparent' })}>
        <View style={{ flexDirection: 'row', gap: 12, alignItems: 'flex-start' }}>
          <Avatar name={a.job?.company ?? '?'} size={44} />
          <View style={{ flex: 1, gap: 2 }}>
            <Text variant="heading" numberOfLines={2}>{a.job?.title ?? 'Oferta'}</Text>
            <Text variant="caption" tone="muted" numberOfLines={1}>{a.job?.company}{a.job?.district ? ` · ${a.job.district}` : ''}</Text>
          </View>
          <Icon name="ellipsis-horizontal" size={20} tone="subtle" />
        </View>

        {upcoming ? (
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8, padding: 10, borderRadius: radius.md, backgroundColor: colors.accentTint }}>
            <Icon name="calendar" size={16} tone="accent" />
            <Text variant="caption" style={{ fontFamily: fontFamily.bold }}>Entrevista: {formatDateTime(a.interviewDate)}</Text>
          </View>
        ) : null}

        <View style={{ flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', columnGap: 12, rowGap: 4 }}>
          {salary ? (
            <View style={{ flexDirection: 'row', alignItems: 'center', gap: 5 }}>
              <Icon name="cash-outline" size={14} tone="subtle" />
              <Text variant="caption" tone="muted">{salary}</Text>
            </View>
          ) : null}
          <Text variant="caption" tone="subtle">
            {a.appliedAt ? `Postulaste ${formatRelativeTime(a.appliedAt)}` : `Actualizada ${formatRelativeTime(a.updatedAt)}`}
          </Text>
        </View>

        {a.notes ? (
          <View style={{ flexDirection: 'row', gap: 6, alignItems: 'flex-start' }}>
            <Icon name="document-text-outline" size={14} tone="subtle" />
            <Text variant="caption" tone="muted" numberOfLines={2} style={{ flex: 1 }}>{a.notes}</Text>
          </View>
        ) : null}
      </Pressable>

      {nextLabel ? (
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8, paddingHorizontal: 12, paddingVertical: 10, borderTopWidth: 1, borderTopColor: colors.border }}>
          <Button label={nextLabel} icon="arrow-forward-circle-outline" size="sm" variant="tonal" onPress={onAdvance} disabled={busy} testID={`advance-${a.id}`} style={{ flex: 1 }} fullWidth />
          <IconButton icon="create-outline" label="Editar seguimiento" size={38} variant="ghost" onPress={onEdit} />
        </View>
      ) : null}
    </Card>
  );
}
