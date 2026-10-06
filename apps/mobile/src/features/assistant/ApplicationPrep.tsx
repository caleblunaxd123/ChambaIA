import * as Clipboard from 'expo-clipboard';
import { useState } from 'react';
import { Pressable, View } from 'react-native';

import { usePrepGenerate, usePrepPreview } from '@/api/queries';
import type { PrepDraft } from '@/api/schemas';
import { haptics } from '@/lib/haptics';
import { toast } from '@/state/toast-store';
import { BottomSheet } from '@/ui/BottomSheet';
import { Button } from '@/ui/Button';
import { Card } from '@/ui/Card';
import { Chip } from '@/ui/Chip';
import { InlineError } from '@/ui/EmptyState';
import { Icon } from '@/ui/Icon';
import { Skeleton } from '@/ui/Skeleton';
import { Text } from '@/ui/Text';
import { fontFamily, useTheme } from '@/ui/theme';

import { type Tone, canGenerate, copyableMessage, quotaLabel, toneLabel } from './helpers';

/**
 * Entry point on the job detail. It only exists when the server has AI switched on: no card, no promise we cannot keep.
 * Opening the sheet sends nothing; the draft is generated only after the person approves the data shown.
 */
export function PrepCard({ jobId }: { jobId: string }) {
  const preview = usePrepPreview(jobId);
  const [open, setOpen] = useState(false);

  if (!preview.data?.available) return null;

  return (
    <>
      <Card onPress={() => { haptics.tap(); setOpen(true); }} testID="prep-card" accessibilityLabel="Preparar mi postulación con IA" style={{ flexDirection: 'row', alignItems: 'center', gap: 12 }}>
        <PrepIcon />
        <View style={{ flex: 1, gap: 2 }}>
          <Text variant="bodyStrong">Preparar mi postulación</Text>
          <Text variant="caption" tone="muted">Un borrador de mensaje y posibles preguntas de entrevista. Tú decides qué se envía a la IA.</Text>
        </View>
        <Icon name="chevron-forward" size={18} tone="subtle" />
      </Card>
      {open ? <PrepSheet jobId={jobId} onClose={() => setOpen(false)} /> : null}
    </>
  );
}

function PrepIcon() {
  const { colors } = useTheme();
  return (
    <View style={{ width: 44, height: 44, borderRadius: 14, backgroundColor: colors.accentTint, alignItems: 'center', justifyContent: 'center' }}>
      <Icon name="sparkles" size={22} tone="accent" />
    </View>
  );
}

function PrepSheet({ jobId, onClose }: { jobId: string; onClose: () => void }) {
  const preview = usePrepPreview(jobId);
  const generate = usePrepGenerate(jobId);
  const [tone, setTone] = useState<Tone>('formal');
  const [showPayload, setShowPayload] = useState(false);
  const { colors, radius } = useTheme();

  const data = preview.data;
  const draft = generate.data;
  const allowed = canGenerate(data);

  const run = () => {
    haptics.tap();
    generate.mutate(tone, { onSuccess: () => haptics.success(), onError: () => haptics.error() });
  };

  const footer = draft ? (
    <View style={{ gap: 8 }}>
      <Button label="Copiar mensaje" icon="copy-outline" onPress={() => void copy(draft.message)} fullWidth testID="prep-copy" />
      <Button label="Generar otra versión" variant="secondary" onPress={run} loading={generate.isPending} disabled={!allowed} fullWidth />
    </View>
  ) : (
    <View style={{ gap: 8 }}>
      <Button label="Aprobar y generar borrador" icon="sparkles" onPress={run} loading={generate.isPending} disabled={!allowed} fullWidth testID="prep-approve" />
      <Button label="Ahora no" variant="ghost" onPress={onClose} fullWidth />
    </View>
  );

  return (
    <BottomSheet visible onClose={onClose} title="Preparar mi postulación" footer={footer}>
      {preview.isLoading || !data ? (
        <View style={{ gap: 10 }}><Skeleton height={20} width="60%" /><Skeleton height={90} radius={14} /></View>
      ) : draft ? (
        <Draft draft={draft} />
      ) : (
        <>
          <Text tone="muted">Tu agente puede redactarte un borrador para el reclutador. Para eso enviará a un modelo de IA <Text variant="bodyStrong">solo esto</Text>:</Text>
          <Bullets items={data.includes} icon="checkmark-circle" tone="success" />
          <Text variant="bodyStrong">Nunca se envía</Text>
          <Bullets items={data.excludes} icon="close-circle" tone="danger" />

          <Pressable onPress={() => setShowPayload((v) => !v)} accessibilityRole="button" testID="prep-show-payload" style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
            <Icon name={showPayload ? 'chevron-up' : 'chevron-down'} size={16} tone="primary" />
            <Text variant="caption" tone="primary" style={{ fontFamily: fontFamily.bold }}>{showPayload ? 'Ocultar el texto exacto' : 'Ver el texto exacto que se enviaría'}</Text>
          </Pressable>
          {showPayload ? (
            <View style={{ padding: 12, borderRadius: radius.md, backgroundColor: colors.surfaceMuted }}>
              <Text variant="caption" selectable testID="prep-payload">{data.payload}</Text>
            </View>
          ) : null}

          <View style={{ gap: 8 }}>
            <Text variant="caption" tone="muted">Tono del mensaje</Text>
            <View style={{ flexDirection: 'row', gap: 8 }}>
              {(['formal', 'cercano'] as const).map((t) => <Chip key={t} label={toneLabel[t]} selected={tone === t} onPress={() => setTone(t)} />)}
            </View>
          </View>

          <Text variant="caption" tone={allowed ? 'muted' : 'danger'} testID="prep-quota">{quotaLabel(data.quota)}</Text>
          <Text variant="caption" tone="subtle">Es solo un borrador: no se envía nada a nadie y no se guarda en ningún lado.</Text>
        </>
      )}
      {generate.error ? <InlineError message={generate.error.message} /> : null}
    </BottomSheet>
  );
}

async function copy(message: string) {
  try {
    await Clipboard.setStringAsync(copyableMessage(message));
    haptics.success();
    toast.show({ message: 'Mensaje copiado. Revísalo antes de usarlo.', tone: 'success' });
  } catch {
    toast.show({ message: 'No pudimos copiar el mensaje.', tone: 'danger' });
  }
}

function Draft({ draft }: { draft: PrepDraft }) {
  const { colors, radius } = useTheme();
  return (
    <View style={{ gap: 14 }} testID="prep-draft">
      {draft.warnings.length > 0 ? (
        <View style={{ padding: 12, gap: 6, borderRadius: radius.md, backgroundColor: colors.warningTint }} testID="prep-warnings">
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
            <Icon name="alert-circle" size={18} tone="warning" />
            <Text variant="bodyStrong" tone="warning">Revisa esto antes de usarlo</Text>
          </View>
          {draft.warnings.map((w) => <Text key={w} variant="caption">• {w}</Text>)}
        </View>
      ) : null}

      <View style={{ padding: 14, borderRadius: radius.md, backgroundColor: colors.surfaceMuted }}>
        <Text selectable testID="prep-message">{draft.message}</Text>
      </View>
      <Text variant="caption" tone="subtle">Borrador generado con IA. Léelo, corrígelo y envíalo tú: no se mandó nada.</Text>

      <Section title="Por qué encajas" items={draft.highlights} icon="checkmark-circle" tone="success" />
      <Section title="Lo que piden y tu perfil no muestra" hint="No lo afirmes si no lo sabes hacer." items={draft.gaps} icon="alert-circle" tone="warning" />
      <Section title="Preguntas que podrían hacerte" items={draft.questions} icon="help-circle" tone="primary" />
    </View>
  );
}

function Section({ title, hint, items, icon, tone }: { title: string; hint?: string; items: string[]; icon: 'checkmark-circle' | 'alert-circle' | 'help-circle'; tone: 'success' | 'warning' | 'primary' }) {
  if (items.length === 0) return null;
  return (
    <View style={{ gap: 8 }}>
      <Text variant="bodyStrong">{title}</Text>
      {hint ? <Text variant="caption" tone="muted">{hint}</Text> : null}
      <Bullets items={items} icon={icon} tone={tone} />
    </View>
  );
}

function Bullets({ items, icon, tone }: { items: string[]; icon: 'checkmark-circle' | 'close-circle' | 'alert-circle' | 'help-circle'; tone: 'success' | 'danger' | 'warning' | 'primary' }) {
  return (
    <View style={{ gap: 6 }}>
      {items.map((item) => (
        <View key={item} style={{ flexDirection: 'row', gap: 8, alignItems: 'flex-start' }}>
          <View style={{ marginTop: 2 }}><Icon name={icon} size={16} tone={tone} /></View>
          <Text style={{ flex: 1 }}>{item}</Text>
        </View>
      ))}
    </View>
  );
}
