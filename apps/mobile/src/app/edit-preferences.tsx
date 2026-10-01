import { zodResolver } from '@hookform/resolvers/zod';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { View } from 'react-native';

import type { Preferences } from '@/api/schemas';
import { usePreferences, useUpdatePreferences } from '@/api/queries';
import { type PreferencesDraft, PreferencesFields, draftFromPreferences } from '@/features/profile/PreferencesFields';
import { type PreferencesForm, preferencesFormSchema, preferencesToInput } from '@/features/profile/schemas';
import { haptics } from '@/lib/haptics';
import { Button } from '@/ui/Button';
import { Card } from '@/ui/Card';
import { ErrorState } from '@/ui/EmptyState';
import { ModalScreen } from '@/ui/ModalScreen';
import { Skeleton } from '@/ui/Skeleton';
import { Text } from '@/ui/Text';

export default function EditPreferencesScreen() {
  const prefs = usePreferences();

  if (prefs.data) return <PreferencesFormView prefs={prefs.data} />;

  return (
    <ModalScreen title="Preferencias" footer={<Button label="Guardar y actualizar mi búsqueda" onPress={() => undefined} disabled fullWidth />}>
      {prefs.isError ? (
        <ErrorState message={prefs.error.message} onRetry={() => void prefs.refetch()} />
      ) : (
        <View style={{ gap: 14 }}><Skeleton height={52} radius={14} /><Skeleton height={52} radius={14} /><Skeleton height={52} radius={14} /></View>
      )}
    </ModalScreen>
  );
}

/** Mounted only once preferences are loaded, so the draft starts from real data (no seeding effects). */
function PreferencesFormView({ prefs }: { prefs: Preferences }) {
  const router = useRouter();
  const update = useUpdatePreferences();
  const [draft, setDraft] = useState<PreferencesDraft>(() => draftFromPreferences(prefs));

  const { control, handleSubmit } = useForm<PreferencesForm>({
    resolver: zodResolver(preferencesFormSchema),
    defaultValues: { minSalary: prefs.minSalary == null ? '' : String(prefs.minSalary) },
  });

  const save = handleSubmit((form) => {
    update.mutate(preferencesToInput(prefs, draft, form), {
      onSuccess: () => {
        haptics.success();
        if (router.canGoBack()) router.back();
      },
      onError: () => haptics.error(),
    });
  });

  return (
    <ModalScreen title="Preferencias" footer={<Button label="Guardar y actualizar mi búsqueda" onPress={save} loading={update.isPending} fullWidth testID="preferences-save" />}>
      <Text tone="muted">Tu agente usa esto para filtrar. Al guardar, revisa de nuevo todas las ofertas.</Text>
      <PreferencesFields control={control} draft={draft} onChange={(key, value) => setDraft((d) => ({ ...d, [key]: value }))} />
      {update.error ? <Card tone="muted"><Text tone="danger">{update.error.message}</Text></Card> : null}
    </ModalScreen>
  );
}
