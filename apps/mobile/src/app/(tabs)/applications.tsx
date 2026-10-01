import { useRouter } from 'expo-router';
import { useMemo, useState } from 'react';
import { View } from 'react-native';

import type { ApplicationStatus, JobApplication } from '@/api/schemas';
import { useApplications, usePatchApplication, useRemoveApplication } from '@/api/queries';
import { APPLICATION_STAGES, ApplicationSheet } from '@/features/applications/ApplicationSheet';
import { applicationStatusLabel, formatRelativeTime, formatSalary } from '@/lib/format';
import { haptics } from '@/lib/haptics';
import { Badge, type BadgeTone } from '@/ui/Badge';
import { Button } from '@/ui/Button';
import { Card } from '@/ui/Card';
import { SegmentedTabs } from '@/ui/Chip';
import { EmptyState, ErrorState } from '@/ui/EmptyState';
import { Icon } from '@/ui/Icon';
import { Screen } from '@/ui/Screen';
import { JobListSkeleton } from '@/ui/Skeleton';
import { Text } from '@/ui/Text';

const stageTone: Record<ApplicationStatus, BadgeTone> = {
  found: 'neutral',
  interested: 'brand',
  applied: 'info',
  interview: 'accent',
  offer: 'success',
  discarded: 'danger',
};

const emptyCopy: Record<ApplicationStatus, { title: string; message: string }> = {
  found: { title: 'Nada por aquí', message: 'Las ofertas que encuentres y quieras seguir aparecerán en esta etapa.' },
  interested: { title: 'Aún no marcas ofertas como «Me interesa»', message: 'Cuando guardes una oferta, la verás aquí lista para postular.' },
  applied: { title: 'Todavía no registras postulaciones', message: 'Después de postular en la página oficial, marca la oferta como postulada para hacerle seguimiento.' },
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

  // Open on the first stage that has cards, so the board never greets the user with an empty column.
  const stage = selectedStage ?? APPLICATION_STAGES.find((s) => counts[s] > 0) ?? 'interested';
  const visible = list.filter((a) => a.status === stage);

  return (
    <Screen onRefresh={() => void apps.refetch()} refreshing={apps.isRefetching}>
      <View style={{ gap: 4 }}>
        <Text variant="display">Postulaciones</Text>
        <Text tone="muted">Sigue cada oferta desde que te interesa hasta que recibes respuesta.</Text>
      </View>

      <SegmentedTabs
        scrollable
        value={stage}
        onChange={setSelectedStage}
        options={APPLICATION_STAGES.map((s) => ({ value: s, label: applicationStatusLabel[s], count: counts[s] }))}
      />

      {apps.isLoading ? (
        <JobListSkeleton count={2} />
      ) : apps.isError ? (
        <ErrorState message={apps.error.message} onRetry={() => void apps.refetch()} />
      ) : visible.length === 0 ? (
        <EmptyState icon="paper-plane-outline" title={emptyCopy[stage].title} message={emptyCopy[stage].message} actionLabel={list.length === 0 ? 'Explorar empleos' : undefined} onAction={() => router.navigate('/jobs')} />
      ) : (
        <View style={{ gap: 12 }}>
          {visible.map((a) => (
            <Card key={a.id} onPress={() => setEditing(a)} testID={`application-${a.id}`} style={{ gap: 10 }}>
              <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 }}>
                <View style={{ flex: 1, gap: 2 }}>
                  <Text variant="heading" numberOfLines={2}>{a.job?.title ?? 'Oferta'}</Text>
                  <Text tone="muted" numberOfLines={1}>{a.job?.company}{a.job?.district ? ` · ${a.job.district}` : ''}</Text>
                </View>
                <Badge label={applicationStatusLabel[a.status]} tone={stageTone[a.status]} />
              </View>

              {a.job && formatSalary(a.job) ? (
                <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
                  <Icon name="cash-outline" size={15} tone="subtle" />
                  <Text variant="caption" tone="muted">{formatSalary(a.job)}</Text>
                </View>
              ) : null}

              <Text variant="caption" tone="subtle">
                {a.appliedAt ? `Postulaste ${formatRelativeTime(a.appliedAt)}` : `Actualizada ${formatRelativeTime(a.updatedAt)}`}
              </Text>
              {a.notes ? <Text variant="caption" tone="muted" numberOfLines={2}>📝 {a.notes}</Text> : null}
              <Button label="Actualizar" size="sm" variant="secondary" icon="swap-horizontal" onPress={() => setEditing(a)} />
            </Card>
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
            { id, status: changes.status, notes: changes.notes },
            {
              onSuccess: (updated) => {
                haptics.success();
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
              setEditing(null);
              setSelectedStage(null);
            },
          })
        }
      />
    </Screen>
  );
}
