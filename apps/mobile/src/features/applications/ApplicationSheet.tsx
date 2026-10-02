import { useState } from 'react';
import { View } from 'react-native';

import { useApplicationHistory } from '@/api/queries';
import type { ApplicationStatus, JobApplication } from '@/api/schemas';
import { applicationStatusLabel, formatSalary } from '@/lib/format';
import { haptics } from '@/lib/haptics';
import { Avatar } from '@/ui/Avatar';
import { BottomSheet } from '@/ui/BottomSheet';
import { Button } from '@/ui/Button';
import { Chip } from '@/ui/Chip';
import { ConfirmDialog } from '@/ui/ConfirmDialog';
import { Input } from '@/ui/Input';
import { Icon } from '@/ui/Icon';
import { Text } from '@/ui/Text';

import { describeEvent } from './history';
import { InterviewPicker } from './InterviewPicker';
import { APPLICATION_STAGES, stageStyle } from './stages';

export { APPLICATION_STAGES } from './stages';

export type ApplicationChanges = { status: ApplicationStatus; notes: string; interviewDate?: string; clearInterviewDate?: boolean };

type Props = {
  application: JobApplication | null;
  saving: boolean;
  onClose: () => void;
  onSave: (id: string, patch: ApplicationChanges) => void;
  onRemove: (id: string) => void;
  onOpenJob: (jobId: string) => void;
};

/** Edit one tracker card: stage, interview date, notes, remove. The draft is re-seeded whenever a different card is opened. */
export function ApplicationSheet({ application, saving, onClose, onSave, onRemove, onOpenJob }: Props) {
  const [status, setStatus] = useState<ApplicationStatus>('interested');
  const [notes, setNotes] = useState('');
  const [interview, setInterview] = useState<Date | null>(null);
  const [seededFor, setSeededFor] = useState<string | null>(null);
  const [confirmRemove, setConfirmRemove] = useState(false);

  if (application && seededFor !== application.id) {
    setSeededFor(application.id);
    setStatus(application.status);
    setNotes(application.notes ?? '');
    setInterview(application.interviewDate ? new Date(application.interviewDate) : null);
  }
  if (!application && seededFor !== null) setSeededFor(null);

  const job = application?.job;
  const salary = job ? formatSalary(job) : null;
  const history = useApplicationHistory(application?.id);
  const events = history.data ?? [];

  return (
    <>
      <BottomSheet
        visible={application !== null}
        onClose={onClose}
        title="Seguimiento"
        footer={
          application ? (
            <View style={{ gap: 8 }}>
              <Button
                label="Guardar cambios"
                onPress={() =>
                  onSave(application.id, {
                    status,
                    notes,
                    interviewDate: status === 'interview' && interview ? interview.toISOString() : undefined,
                    clearInterviewDate: application.interviewDate !== null && interview === null ? true : undefined,
                  })
                }
                loading={saving}
                fullWidth
                testID="application-save"
              />
              <View style={{ flexDirection: 'row', gap: 8 }}>
                <Button label="Ver oferta" variant="secondary" icon="open-outline" onPress={() => { onClose(); onOpenJob(application.jobId); }} style={{ flex: 1 }} />
                <Button label="Quitar" variant="danger" icon="trash-outline" onPress={() => { haptics.warning(); setConfirmRemove(true); }} style={{ flex: 1 }} testID="application-remove" />
              </View>
            </View>
          ) : undefined
        }
      >
        {job ? (
          <View style={{ flexDirection: 'row', gap: 12, alignItems: 'center' }}>
            <Avatar name={job.company} size={48} />
            <View style={{ flex: 1 }}>
              <Text variant="heading" numberOfLines={2}>{job.title}</Text>
              <Text variant="caption" tone="muted" numberOfLines={1}>{job.company}{salary ? ` · ${salary}` : ''}</Text>
            </View>
          </View>
        ) : null}

        <View style={{ gap: 8 }}>
          <Text variant="caption" tone="muted">¿En qué etapa está?</Text>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
            {APPLICATION_STAGES.map((stage) => (
              <Chip key={stage} label={applicationStatusLabel[stage]} icon={stageStyle[stage].icon} selected={status === stage} onPress={() => setStatus(stage)} testID={`stage-${stage}`} />
            ))}
          </View>
        </View>

        {status === 'interview' ? <InterviewPicker value={interview} onChange={setInterview} onClear={() => setInterview(null)} /> : null}

        <Input label="Notas" value={notes} onChangeText={setNotes} placeholder="Contacto, qué te preguntaron, pretensión salarial…" multiline numberOfLines={4} maxLength={2000} />

        {events.length > 0 ? (
          <View style={{ gap: 10 }} testID="application-history">
            <Text variant="caption" tone="muted">Historial</Text>
            {events.slice(0, 8).map((event) => {
              const line = describeEvent(event);
              return (
                <View key={event.id} style={{ flexDirection: 'row', gap: 10, alignItems: 'flex-start' }}>
                  <View style={{ marginTop: 2 }}><Icon name={line.icon} size={16} tone="muted" /></View>
                  <View style={{ flex: 1 }}>
                    <Text>{line.text}</Text>
                    <Text variant="caption" tone="subtle">{line.when}</Text>
                  </View>
                </View>
              );
            })}
          </View>
        ) : null}
      </BottomSheet>

      <ConfirmDialog
        visible={confirmRemove}
        title="¿Quitar del tablero?"
        message="La oferta seguirá disponible en Empleos; solo se borra tu seguimiento y tus notas."
        confirmLabel="Quitar"
        destructive
        onCancel={() => setConfirmRemove(false)}
        onConfirm={() => {
          setConfirmRemove(false);
          if (application) onRemove(application.id);
        }}
      />
    </>
  );
}
