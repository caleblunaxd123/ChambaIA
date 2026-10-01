import { useState } from 'react';
import { View } from 'react-native';

import type { ApplicationStatus, JobApplication } from '@/api/schemas';
import { applicationStatusLabel } from '@/lib/format';
import { haptics } from '@/lib/haptics';
import { BottomSheet } from '@/ui/BottomSheet';
import { Button } from '@/ui/Button';
import { Chip } from '@/ui/Chip';
import { ConfirmDialog } from '@/ui/ConfirmDialog';
import { Input } from '@/ui/Input';
import { Text } from '@/ui/Text';

export const APPLICATION_STAGES: ApplicationStatus[] = ['found', 'interested', 'applied', 'interview', 'offer', 'discarded'];

type Props = {
  application: JobApplication | null;
  saving: boolean;
  onClose: () => void;
  onSave: (id: string, patch: { status: ApplicationStatus; notes: string }) => void;
  onRemove: (id: string) => void;
  onOpenJob: (jobId: string) => void;
};

/** Edit one tracker card: stage, notes, remove. The draft is re-seeded whenever a different card is opened. */
export function ApplicationSheet({ application, saving, onClose, onSave, onRemove, onOpenJob }: Props) {
  const [status, setStatus] = useState<ApplicationStatus>('interested');
  const [notes, setNotes] = useState('');
  const [seededFor, setSeededFor] = useState<string | null>(null);
  const [confirmRemove, setConfirmRemove] = useState(false);

  if (application && seededFor !== application.id) {
    setSeededFor(application.id);
    setStatus(application.status);
    setNotes(application.notes ?? '');
  }
  if (!application && seededFor !== null) setSeededFor(null);

  return (
    <>
      <BottomSheet
        visible={application !== null}
        onClose={onClose}
        title={application?.job?.title ?? 'Postulación'}
        footer={
          application ? (
            <View style={{ gap: 8 }}>
              <Button label="Guardar cambios" onPress={() => onSave(application.id, { status, notes })} loading={saving} fullWidth testID="application-save" />
              <View style={{ flexDirection: 'row', gap: 8 }}>
                <Button label="Ver oferta" variant="secondary" icon="open-outline" onPress={() => { onClose(); onOpenJob(application.jobId); }} style={{ flex: 1 }} />
                <Button label="Quitar" variant="danger" icon="trash-outline" onPress={() => { haptics.warning(); setConfirmRemove(true); }} style={{ flex: 1 }} />
              </View>
            </View>
          ) : undefined
        }
      >
        {application?.job ? <Text tone="muted">{application.job.company}</Text> : null}

        <View style={{ gap: 8 }}>
          <Text variant="caption" tone="muted">Etapa</Text>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
            {APPLICATION_STAGES.map((stage) => (
              <Chip key={stage} label={applicationStatusLabel[stage]} selected={status === stage} onPress={() => setStatus(stage)} testID={`stage-${stage}`} />
            ))}
          </View>
        </View>

        <Input label="Notas" value={notes} onChangeText={setNotes} placeholder="Contacto, fecha de la entrevista, pretensión salarial…" multiline numberOfLines={4} maxLength={2000} />
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
