import { zodResolver } from '@hookform/resolvers/zod';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { View } from 'react-native';

import type { EducationLevel, EducationStatus, Profile, Skill } from '@/api/schemas';
import { useProfile, useUpdateProfile } from '@/api/queries';
import { SkillEditor } from '@/features/profile/SkillEditor';
import { type ProfileForm, formToProfileInput, profileFormSchema, profileToForm } from '@/features/profile/schemas';
import { educationLabel } from '@/lib/format';
import { haptics } from '@/lib/haptics';
import { toast } from '@/state/toast-store';
import { Button } from '@/ui/Button';
import { ErrorState, InlineError } from '@/ui/EmptyState';
import { FormInput } from '@/ui/Input';
import { ModalScreen } from '@/ui/ModalScreen';
import { Select } from '@/ui/Select';
import { Skeleton } from '@/ui/Skeleton';
import { Text } from '@/ui/Text';

const LEVEL_OPTIONS = (Object.keys(educationLabel) as EducationLevel[]).map((v) => ({ value: v, label: educationLabel[v] }));
const STATUS_OPTIONS: { value: EducationStatus; label: string }[] = [
  { value: 'inProgress', label: 'En curso' },
  { value: 'completed', label: 'Terminado' },
];

export default function EditProfileScreen() {
  const profile = useProfile();

  if (profile.data) return <ProfileFormView profile={profile.data} />;

  return (
    <ModalScreen title="Tu perfil" footer={<Button label="Guardar perfil" onPress={() => undefined} disabled fullWidth />}>
      {profile.isError ? (
        <ErrorState message={profile.error.message} onRetry={() => void profile.refetch()} />
      ) : (
        <View style={{ gap: 14 }}><Skeleton height={52} radius={14} /><Skeleton height={52} radius={14} /><Skeleton height={120} radius={14} /></View>
      )}
    </ModalScreen>
  );
}

/** Mounted only once the profile is loaded, so every field starts from real data (no seeding effects). */
function ProfileFormView({ profile }: { profile: Profile }) {
  const router = useRouter();
  const update = useUpdateProfile();
  const [skills, setSkills] = useState<Skill[]>(profile.skills);
  const [level, setLevel] = useState<EducationLevel | null>(profile.educationLevel);
  const [status, setStatus] = useState<EducationStatus | null>(profile.educationStatus);

  const { control, handleSubmit, formState } = useForm<ProfileForm>({
    resolver: zodResolver(profileFormSchema),
    defaultValues: profileToForm(profile),
  });
  const dirty = formState.isDirty || skills !== profile.skills || level !== profile.educationLevel || status !== profile.educationStatus;

  const save = handleSubmit((form) => {
    update.mutate(formToProfileInput(form, profile, { skills, educationLevel: level, educationStatus: status }), {
      onSuccess: () => {
        haptics.success();
        toast.show({ message: 'Perfil guardado. Tu agente ya está revisando tus ofertas.', tone: 'success' });
        if (router.canGoBack()) router.back();
      },
      onError: () => haptics.error(),
    });
  });

  return (
    <ModalScreen
      title="Tu perfil"
      subtitle="Lo que tu agente sabe de ti. Siempre puedes corregirlo."
      dirty={dirty}
      footer={<Button label={dirty ? 'Guardar perfil' : 'Sin cambios'} onPress={save} loading={update.isPending} disabled={!dirty} fullWidth testID="profile-save" />}
    >

      <View style={{ gap: 14 }}>
        <FormInput control={control} name="fullName" label="Nombre completo" icon="person-outline" autoCapitalize="words" />
        <FormInput control={control} name="headline" label="Titular profesional" placeholder="Ej.: Asistente administrativa con experiencia en facturación" icon="ribbon-outline" />
      </View>

      <View style={{ gap: 10 }}>
        <Text variant="heading">Experiencia total</Text>
        <Text variant="caption" tone="muted">Suma todos tus trabajos. Tu agente la compara con lo que pide cada oferta.</Text>
        <View style={{ flexDirection: 'row', gap: 12 }}>
          <View style={{ flex: 1 }}><FormInput control={control} name="years" label="Años" keyboardType="number-pad" suffix="años" /></View>
          <View style={{ flex: 1 }}><FormInput control={control} name="months" label="Meses" keyboardType="number-pad" suffix="meses" /></View>
        </View>
      </View>

      <View style={{ gap: 12 }}>
        <Text variant="heading">Estudios</Text>
        <Select label="Nivel más alto" options={LEVEL_OPTIONS} value={level} onChange={setLevel} clearLabel="Prefiero no indicarlo" placeholder="Selecciona" />
        {level ? <Select label="Estado" options={STATUS_OPTIONS} value={status} onChange={setStatus} placeholder="Selecciona" /> : null}
      </View>

      <View style={{ gap: 10 }}>
        <Text variant="heading">Habilidades</Text>
        <SkillEditor skills={skills} onChange={setSkills} />
      </View>

      {update.error ? <InlineError message={update.error.message} /> : null}
    </ModalScreen>
  );
}
