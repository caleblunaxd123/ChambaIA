import { zodResolver } from '@hookform/resolvers/zod';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { KeyboardAvoidingView, Platform, View } from 'react-native';

import {
  useOverview, usePreferences, useProfile, useUpdatePreferences, useUpdateProfile, useUploadResume,
} from '@/api/queries';
import type { DetectedResume, Preferences, Profile } from '@/api/schemas';
import { AboutStep, CvStep, Heading, ProcessingStep, ReadyStep, ReviewStep } from '@/features/onboarding/Steps';
import { type ProfileDraft, applyDetected, draftFromProfile, draftToProfileInput, unionText } from '@/features/onboarding/mapping';
import { CvPickError, pickCv } from '@/features/onboarding/pickCv';
import { type PreferencesDraft, PreferencesFields, draftFromPreferences } from '@/features/profile/PreferencesFields';
import { type PreferencesForm, preferencesFormSchema, preferencesToInput } from '@/features/profile/schemas';
import { firstName } from '@/lib/format';
import { haptics } from '@/lib/haptics';
import { useAuthStore } from '@/state/auth-store';
import { Button, IconButton } from '@/ui/Button';
import { ErrorState, InlineError } from '@/ui/EmptyState';
import { Screen } from '@/ui/Screen';
import { Skeleton } from '@/ui/Skeleton';
import { Text } from '@/ui/Text';
import { fontFamily, useTheme } from '@/ui/theme';

type Step = 'about' | 'cv' | 'processing' | 'review' | 'prefs' | 'ready';

/** Minimum time on the "processing" screen so the animation reads as work, not as a flicker. */
const MIN_PROCESSING_MS = 2200;
const wait = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms));

export default function OnboardingScreen() {
  const profile = useProfile();
  const prefs = usePreferences();

  if (profile.data && prefs.data) return <OnboardingFlow profile={profile.data} prefs={prefs.data} />;

  return (
    <Screen>
      {profile.isError || prefs.isError ? (
        <ErrorState message={(profile.error ?? prefs.error)?.message} onRetry={() => { void profile.refetch(); void prefs.refetch(); }} />
      ) : (
        <View style={{ gap: 16, paddingTop: 40 }}><Skeleton height={40} width="70%" /><Skeleton height={52} radius={14} /><Skeleton height={52} radius={14} /></View>
      )}
    </Screen>
  );
}

function OnboardingFlow({ profile, prefs }: { profile: Profile; prefs: Preferences }) {
  const router = useRouter();
  const { colors, radius } = useTheme();
  const { mode } = useLocalSearchParams<{ mode?: string }>();
  // Re-uploading a CV from the profile reuses the same flow without the intro and preferences steps.
  const cvOnly = mode === 'cv' || profile.onboardingCompleted;
  const user = useAuthStore((s) => s.user);

  const [step, setStep] = useState<Step>(cvOnly ? 'cv' : 'about');
  const [draft, setDraft] = useState<ProfileDraft>(() => draftFromProfile(profile, user?.fullName ?? '', prefs.preferredRoles));
  const [prefsDraft, setPrefsDraft] = useState<PreferencesDraft>(() => draftFromPreferences(prefs));
  const [detected, setDetected] = useState<DetectedResume | null>(null);
  const [cvError, setCvError] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);

  const upload = useUploadResume();
  const updateProfile = useUpdateProfile();
  const updatePreferences = useUpdatePreferences();
  const overview = useOverview();

  const flow: Step[] = cvOnly ? ['cv', 'review', 'ready'] : ['about', 'cv', 'review', 'prefs', 'ready'];
  const position = Math.max(0, flow.indexOf(step === 'processing' ? 'cv' : step));

  const close = () => (router.canGoBack() ? router.back() : router.replace('/profile'));

  const choosePdf = async () => {
    setCvError(null);
    try {
      const picked = await pickCv();
      if (!picked) return;
      setStep('processing');
      const [result] = await Promise.all([upload.mutateAsync(picked.form), wait(MIN_PROCESSING_MS)]);
      setDetected(result.detected);
      if (result.detected) setDraft((d) => applyDetected(d, result.detected as DetectedResume));
      haptics.success();
      setStep('review');
    } catch (error) {
      haptics.error();
      setCvError(error instanceof CvPickError || error instanceof Error ? error.message : 'No pudimos procesar tu CV.');
      setStep('cv');
    }
  };

  const saveProfile = async () => {
    setSaveError(null);
    try {
      await updateProfile.mutateAsync(draftToProfileInput(draft, false));
      if (cvOnly) {
        // Keep the targeted roles in sync with the reviewed CV.
        await updatePreferences.mutateAsync(preferencesToInput(prefs, { preferredRoles: draft.roles }, { minSalary: prefs.minSalary == null ? '' : String(prefs.minSalary) }));
        setStep('ready');
      } else setStep('prefs');
    } catch (error) {
      haptics.error();
      setSaveError(error instanceof Error ? error.message : 'No pudimos guardar tus datos.');
    }
  };

  const finish = async () => {
    if (cvOnly) return close();
    try {
      await updateProfile.mutateAsync(draftToProfileInput(draft, true));
      haptics.success();
      router.replace('/');
    } catch {
      haptics.error();
    }
  };

  return (
    <KeyboardAvoidingView style={{ flex: 1, backgroundColor: colors.bg }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <Screen contentStyle={{ gap: 20, paddingBottom: 48 }}>
        <View style={{ gap: 10 }}>
          <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
            <BackButton visible={step === 'cv' && !cvOnly || step === 'review' || step === 'prefs'} onPress={() => setStep(step === 'cv' ? 'about' : step === 'review' ? 'cv' : 'review')} />
            {cvOnly && step !== 'processing' ? (
              <IconButton icon="close" label="Cerrar" onPress={close} variant="ghost" />
            ) : (
              <Text variant="caption" tone="subtle" style={{ marginLeft: 'auto', fontFamily: fontFamily.bold }}>Paso {position + 1} de {flow.length}</Text>
            )}
          </View>
          <View style={{ flexDirection: 'row', gap: 6 }}>
            {flow.map((s, i) => (
              <View key={s} style={{ flex: 1, height: 5, borderRadius: radius.pill, backgroundColor: i <= position ? colors.primary : colors.border }} />
            ))}
          </View>
        </View>

        {step === 'about' ? (
          <AboutStep
            fullName={draft.fullName}
            homeDistrict={prefsDraft.homeDistrict}
            onSubmit={({ fullName, desiredRole, homeDistrict }) => {
              setDraft((d) => ({ ...d, fullName, roles: unionText(d.roles, desiredRole ? [desiredRole] : []) }));
              setPrefsDraft((p) => ({ ...p, homeDistrict }));
              setStep('cv');
            }}
          />
        ) : null}

        {step === 'cv' ? (
          <CvStep
            error={cvError}
            onPick={() => void choosePdf()}
            onManual={() => {
              setDetected(null);
              setStep('review');
            }}
          />
        ) : null}

        {step === 'processing' ? <ProcessingStep /> : null}

        {step === 'review' ? (
          <ReviewStep draft={draft} detected={detected} onChange={setDraft} onContinue={() => void saveProfile()} saving={updateProfile.isPending || updatePreferences.isPending} error={saveError} />
        ) : null}

        {step === 'prefs' ? (
          <PreferencesStep
            prefs={prefs}
            draft={prefsDraft}
            roles={draft.roles}
            onChange={(key, value) => setPrefsDraft((p) => ({ ...p, [key]: value }))}
            saving={updatePreferences.isPending}
            onDone={async (form) => {
              setSaveError(null);
              try {
                await updatePreferences.mutateAsync(preferencesToInput(prefs, { ...prefsDraft, preferredRoles: draft.roles }, form));
                setStep('ready');
              } catch (error) {
                haptics.error();
                setSaveError(error instanceof Error ? error.message : 'No pudimos guardar tus preferencias.');
              }
            }}
            error={saveError}
          />
        ) : null}

        {step === 'ready' ? (
          <ReadyStep overview={overview.data} loading={overview.isLoading} saving={updateProfile.isPending} onFinish={() => void finish()} firstName={firstName(draft.fullName)} />
        ) : null}
      </Screen>
    </KeyboardAvoidingView>
  );
}

function BackButton({ visible, onPress }: { visible: boolean; onPress: () => void }) {
  if (!visible) return <View style={{ height: 44 }} />;
  return <IconButton icon="chevron-back" label="Volver al paso anterior" onPress={onPress} testID="onboarding-back" />;
}

type PreferencesStepProps = {
  prefs: Preferences;
  draft: PreferencesDraft;
  roles: string[];
  onChange: <K extends keyof PreferencesDraft>(key: K, value: PreferencesDraft[K]) => void;
  onDone: (form: PreferencesForm) => void;
  saving: boolean;
  error: string | null;
};

function PreferencesStep({ prefs, draft, onChange, onDone, saving, error }: PreferencesStepProps) {
  const { control, handleSubmit } = useForm<PreferencesForm>({
    resolver: zodResolver(preferencesFormSchema),
    defaultValues: { minSalary: prefs.minSalary == null ? '' : String(prefs.minSalary) },
  });

  return (
    <View style={{ gap: 24 }}>
      <Heading icon="options-outline" title="¿Qué buscas exactamente?" subtitle="Tu agente descartará lo que no sirva. Siempre puedes cambiarlo o decírselo por chat." />
      <PreferencesFields control={control} draft={draft} onChange={onChange} showRoles={false} showAlerts={false} />
      {error ? <InlineError message={error} /> : null}
      <Button label="Guardar y buscar" icon="search" onPress={handleSubmit(onDone)} loading={saving} fullWidth testID="onboarding-prefs-next" />
    </View>
  );
}
