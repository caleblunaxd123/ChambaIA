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
import { toast } from '@/state/toast-store';
import { Button } from '@/ui/Button';
import { ErrorState, InlineError } from '@/ui/EmptyState';
import { ModalScreen } from '@/ui/ModalScreen';
import { Skeleton } from '@/ui/Skeleton';

export default function EditPreferencesScreen() {
  const prefs = usePreferences();

  if (prefs.data) return <PreferencesFormView prefs={prefs.data} />;

  return (
    <ModalScreen title="Lo que busco" footer={<Button label="Guardar y actualizar mi búsqueda" onPress={() => undefined} disabled fullWidth />}>
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
  const [initial] = useState<PreferencesDraft>(() => draftFromPreferences(prefs));
  const [draft, setDraft] = useState<PreferencesDraft>(initial);

  const { control, handleSubmit, formState } = useForm<PreferencesForm>({
    resolver: zodResolver(preferencesFormSchema),
    defaultValues: { minSalary: prefs.minSalary == null ? '' : String(prefs.minSalary) },
  });

  const dirty = formState.isDirty || JSON.stringify(draft) !== JSON.stringify(initial);

  const save = handleSubmit((form) => {
    update.mutate(preferencesToInput(prefs, draft, form), {
      onSuccess: () => {
        haptics.success();
        toast.show({ message: 'Listo. Actualizamos tus ofertas con tus nuevas preferencias.', tone: 'success' });
        if (router.canGoBack()) router.back();
      },
      onError: () => haptics.error(),
    });
  });

  return (
    <ModalScreen
      title="Lo que busco"
      subtitle="Tu agente filtra con esto. Al guardar, revisa de nuevo todas las ofertas."
      dirty={dirty}
      footer={<Button label={dirty ? 'Guardar y actualizar mi búsqueda' : 'Sin cambios'} onPress={save} loading={update.isPending} disabled={!dirty} fullWidth testID="preferences-save" />}
    >
      <PreferencesFields control={control} draft={draft} onChange={(key, value) => setDraft((d) => ({ ...d, [key]: value }))} />
      {update.error ? <InlineError message={update.error.message} /> : null}
    </ModalScreen>
  );
}
