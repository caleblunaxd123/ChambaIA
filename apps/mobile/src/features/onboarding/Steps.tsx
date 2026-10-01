import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { Animated, View } from 'react-native';
import { z } from 'zod';

import { useDistricts } from '@/api/queries';
import type { DetectedResume, EducationLevel, EducationStatus, Overview, Skill } from '@/api/schemas';
import { SkillEditor } from '@/features/profile/SkillEditor';
import { educationLabel } from '@/lib/format';
import { Button } from '@/ui/Button';
import { Card } from '@/ui/Card';
import { Chip } from '@/ui/Chip';
import { FormInput, Input } from '@/ui/Input';
import { Icon } from '@/ui/Icon';
import { Select } from '@/ui/Select';
import { Skeleton } from '@/ui/Skeleton';
import { TagInput } from '@/ui/TagInput';
import { Text } from '@/ui/Text';
import { useTheme } from '@/ui/theme';

import { type ProfileDraft, formatPeriod } from './mapping';

// ---------------------------------------------------------------- Step 2: about you

const aboutSchema = z.object({
  fullName: z.string().trim().min(2, 'Cuéntanos tu nombre.').max(100, 'Es demasiado largo.'),
  desiredRole: z.string().trim().max(80, 'Máximo 80 caracteres.'),
});
type AboutForm = z.infer<typeof aboutSchema>;

type AboutProps = {
  fullName: string;
  homeDistrict: string | null;
  onSubmit: (values: { fullName: string; desiredRole: string; homeDistrict: string | null }) => void;
};

export function AboutStep({ fullName, homeDistrict, onSubmit }: AboutProps) {
  const districts = useDistricts();
  const [district, setDistrict] = useState<string | null>(homeDistrict);
  const { control, handleSubmit } = useForm<AboutForm>({ resolver: zodResolver(aboutSchema), defaultValues: { fullName, desiredRole: '' } });

  return (
    <View style={{ gap: 20 }}>
      <Heading title="Cuéntanos sobre ti" subtitle="Unos datos para que tu agente empiece con el pie derecho." />
      <FormInput control={control} name="fullName" label="¿Cómo te llamas?" icon="person-outline" autoCapitalize="words" />
      <FormInput control={control} name="desiredRole" label="¿Qué trabajo buscas? (opcional)" icon="briefcase-outline" placeholder="Ej.: Asistente administrativa" hint="Puedes cambiarlo o agregar más cargos después." />
      <Select
        label="¿En qué distrito vives?"
        options={(districts.data ?? []).map((d) => ({ value: d, label: d }))}
        value={district}
        onChange={setDistrict}
        clearLabel="Prefiero no indicarlo"
        placeholder="Selecciona tu distrito"
      />
      <Button label="Continuar" onPress={handleSubmit((v) => onSubmit({ ...v, homeDistrict: district }))} fullWidth testID="onboarding-about-next" />
    </View>
  );
}

// ---------------------------------------------------------------- Step 3: CV

type CvProps = { error: string | null; onPick: () => void; onManual: () => void };

export function CvStep({ error, onPick, onManual }: CvProps) {
  const { colors, radius } = useTheme();
  return (
    <View style={{ gap: 20 }}>
      <Heading title="Sube tu CV una sola vez" subtitle="Lo leeremos por ti: así no tienes que llenar nada a mano." />

      <Card style={{ alignItems: 'center', gap: 14, paddingVertical: 28, borderStyle: 'dashed', borderColor: colors.borderStrong, borderWidth: 1.5, borderRadius: radius.xl }}>
        <View style={{ width: 72, height: 72, borderRadius: 36, backgroundColor: colors.primaryTint, alignItems: 'center', justifyContent: 'center' }}>
          <Icon name="document-text-outline" size={34} tone="primary" />
        </View>
        <Text variant="heading">PDF o Word (.docx)</Text>
        <Text variant="caption" tone="muted" style={{ textAlign: 'center' }}>Hasta 5 MB. Si es una foto o un escaneo, sube el archivo original.</Text>
        <Button label="Elegir mi CV" icon="cloud-upload-outline" onPress={onPick} testID="onboarding-pick-cv" style={{ alignSelf: 'center' }} />
      </Card>

      {error ? (
        <Card tone="muted"><Text tone="danger" testID="onboarding-cv-error">{error}</Text></Card>
      ) : null}

      <View style={{ gap: 6 }}>
        <Text variant="caption" tone="muted" style={{ textAlign: 'center' }}>
          Tu CV se analiza una sola vez en nuestros servidores y puedes eliminarlo cuando quieras desde tu perfil.
        </Text>
        <Button label="Prefiero llenarlo a mano" variant="ghost" onPress={onManual} fullWidth testID="onboarding-manual" />
      </View>
    </View>
  );
}

// ---------------------------------------------------------------- Step 4: processing

const MESSAGES = ['Estamos conociendo tu experiencia…', 'Identificando tus habilidades…', 'Buscando los cargos que van contigo…'];

export function ProcessingStep() {
  const { colors } = useTheme();
  const [index, setIndex] = useState(0);
  const [pulse] = useState(() => new Animated.Value(0));

  useEffect(() => {
    const id = setInterval(() => setIndex((i) => (i + 1) % MESSAGES.length), 1500);
    const loop = Animated.loop(Animated.sequence([
      Animated.timing(pulse, { toValue: 1, duration: 900, useNativeDriver: true }),
      Animated.timing(pulse, { toValue: 0, duration: 900, useNativeDriver: true }),
    ]));
    loop.start();
    return () => {
      clearInterval(id);
      loop.stop();
    };
  }, [pulse]);

  return (
    <View style={{ alignItems: 'center', gap: 28, paddingVertical: 60 }} testID="onboarding-processing">
      <View style={{ width: 140, height: 140, alignItems: 'center', justifyContent: 'center' }}>
        <Animated.View
          style={{
            position: 'absolute', width: 140, height: 140, borderRadius: 70, backgroundColor: colors.primaryTint,
            opacity: pulse.interpolate({ inputRange: [0, 1], outputRange: [0.4, 1] }),
            transform: [{ scale: pulse.interpolate({ inputRange: [0, 1], outputRange: [0.85, 1.08] }) }],
          }}
        />
        <View style={{ width: 84, height: 84, borderRadius: 42, backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center' }}>
          <Icon name="sparkles" size={38} tone="onPrimary" />
        </View>
      </View>
      <View style={{ gap: 6, alignItems: 'center' }}>
        <Text variant="title" style={{ textAlign: 'center' }}>{MESSAGES[index]}</Text>
        <Text tone="muted" style={{ textAlign: 'center' }}>Esto toma solo unos segundos.</Text>
      </View>
    </View>
  );
}

// ---------------------------------------------------------------- Step 5: review

type ReviewProps = {
  draft: ProfileDraft;
  detected: DetectedResume | null;
  onChange: (next: ProfileDraft) => void;
  onContinue: () => void;
  saving: boolean;
  error: string | null;
};

const LEVELS = (Object.keys(educationLabel) as EducationLevel[]).map((v) => ({ value: v, label: educationLabel[v] }));
const STATUSES: { value: EducationStatus; label: string }[] = [
  { value: 'inProgress', label: 'En curso' },
  { value: 'completed', label: 'Terminado' },
];

export function ReviewStep({ draft, detected, onChange, onContinue, saving, error }: ReviewProps) {
  const { colors } = useTheme();
  const set = <K extends keyof ProfileDraft>(key: K, value: ProfileDraft[K]) => onChange({ ...draft, [key]: value });

  return (
    <View style={{ gap: 24 }}>
      <Heading
        title={detected ? 'Esto es lo que encontramos' : 'Cuéntanos tu experiencia'}
        subtitle={detected ? 'Revísalo con calma: puedes corregir, quitar o agregar lo que quieras. Nada se guarda hasta que lo confirmes.' : 'Completa lo que sepas. Siempre podrás cambiarlo después.'}
      />

      {detected && detected.warnings.length > 0 ? (
        <Card tone="muted" style={{ gap: 8 }} testID="onboarding-warnings">
          {detected.warnings.map((w) => (
            <View key={w} style={{ flexDirection: 'row', gap: 8, alignItems: 'flex-start' }}>
              <Icon name="information-circle" size={18} tone="warning" />
              <Text variant="caption" tone="muted" style={{ flex: 1 }}>{w}</Text>
            </View>
          ))}
        </Card>
      ) : null}

      <Block title="Puestos sugeridos" hint="Los cargos que tu agente va a buscar por ti.">
        <TagInput label="Cargos" values={draft.roles} onChange={(v) => set('roles', v)} placeholder="Agrega otro cargo" />
      </Block>

      <Block title="Experiencia">
        <View style={{ flexDirection: 'row', gap: 12 }}>
          <View style={{ flex: 1 }}><NumberField label="Años" value={draft.years} onChange={(v) => set('years', v)} /></View>
          <View style={{ flex: 1 }}><NumberField label="Meses" value={draft.months} onChange={(v) => set('months', v)} /></View>
        </View>
        {draft.experience.length > 0 ? (
          <View style={{ gap: 8 }}>
            {draft.experience.map((e, i) => (
              <View key={`${e.title}-${i}`} style={{ flexDirection: 'row', alignItems: 'flex-start', gap: 10, padding: 12, borderRadius: 14, backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border }}>
                <View style={{ flex: 1 }}>
                  <Text variant="bodyStrong">{e.title}</Text>
                  <Text variant="caption" tone="muted">{[e.company, formatPeriod(e.startDate, e.endDate)].filter(Boolean).join(' · ')}</Text>
                </View>
                <Chip label="Quitar" onRemove={() => set('experience', draft.experience.filter((_, j) => j !== i))} />
              </View>
            ))}
          </View>
        ) : null}
      </Block>

      <Block title="Estudios">
        <Select label="Nivel más alto" options={LEVELS} value={draft.educationLevel} onChange={(v) => set('educationLevel', v)} clearLabel="Prefiero no indicarlo" placeholder="Selecciona" />
        {draft.educationLevel ? <Select label="Estado" options={STATUSES} value={draft.educationStatus} onChange={(v) => set('educationStatus', v)} placeholder="Selecciona" /> : null}
      </Block>

      <Block title="Habilidades" hint="Los niveles son una estimación. Ajústalos para que reflejen lo que sabes hacer.">
        <SkillEditor skills={draft.skills} onChange={(skills: Skill[]) => set('skills', skills)} />
      </Block>

      {draft.languages.length > 0 ? (
        <Block title="Idiomas">
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
            {draft.languages.map((l) => (
              <Chip key={l.name} label={l.level ? `${l.name} · ${l.level}` : l.name} onRemove={() => set('languages', draft.languages.filter((x) => x.name !== l.name))} />
            ))}
          </View>
        </Block>
      ) : null}

      {draft.certifications.length > 0 ? (
        <Block title="Cursos y certificaciones">
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
            {draft.certifications.map((c) => (
              <Chip key={c} label={c} onRemove={() => set('certifications', draft.certifications.filter((x) => x !== c))} />
            ))}
          </View>
        </Block>
      ) : null}

      {error ? <Card tone="muted"><Text tone="danger">{error}</Text></Card> : null}
      <Button label="Todo bien, continuar" onPress={onContinue} loading={saving} fullWidth testID="onboarding-review-next" />
    </View>
  );
}

// ---------------------------------------------------------------- Step 7: ready

export function ReadyStep({ overview, loading, saving, onFinish, firstName }: { overview: Overview | undefined; loading: boolean; saving: boolean; onFinish: () => void; firstName: string }) {
  const { colors } = useTheme();
  return (
    <View style={{ alignItems: 'center', gap: 20, paddingTop: 24 }} testID="onboarding-ready">
      <View style={{ width: 96, height: 96, borderRadius: 48, backgroundColor: colors.successTint, alignItems: 'center', justifyContent: 'center' }}>
        <Icon name="checkmark-circle" size={56} tone="success" />
      </View>
      <View style={{ gap: 6, alignItems: 'center' }}>
        <Text variant="display" style={{ textAlign: 'center' }}>Tu agente está listo{firstName ? `, ${firstName}` : ''}</Text>
        <Text tone="muted" style={{ textAlign: 'center' }}>Ya empezó a revisar las ofertas con lo que nos contaste. Seguirá buscando aunque cierres la app.</Text>
      </View>

      <Card style={{ width: '100%', gap: 10 }}>
        {loading || !overview ? (
          <Skeleton height={44} radius={12} />
        ) : (
          <>
            <Text variant="heading">Para empezar, encontramos</Text>
            <Row icon="star" color={colors.success} text={`${overview.strong} que encajan muy bien contigo`} />
            <Row icon="checkmark-circle" color={colors.info} text={`${overview.possible} con requisitos que podrías cumplir`} />
            <Row icon="eye" color={colors.warning} text={`${overview.review} para revisar con calma`} />
          </>
        )}
      </Card>

      <Button label="Ver mis oportunidades" icon="arrow-forward" onPress={onFinish} loading={saving} fullWidth testID="onboarding-finish" />
    </View>
  );
}

// ---------------------------------------------------------------- shared bits

export function Heading({ title, subtitle }: { title: string; subtitle: string }) {
  return (
    <View style={{ gap: 6 }}>
      <Text variant="display">{title}</Text>
      <Text tone="muted">{subtitle}</Text>
    </View>
  );
}

function Block({ title, hint, children }: { title: string; hint?: string; children: React.ReactNode }) {
  return (
    <View style={{ gap: 12 }}>
      <View style={{ gap: 2 }}>
        <Text variant="heading">{title}</Text>
        {hint ? <Text variant="caption" tone="muted">{hint}</Text> : null}
      </View>
      {children}
    </View>
  );
}

function NumberField({ label, value, onChange }: { label: string; value: string; onChange: (v: string) => void }) {
  // Digits only, so the draft always holds a valid number.
  return <Input label={label} value={value} onChangeText={(v) => onChange(v.replace(/D/g, '').slice(0, 2))} keyboardType="number-pad" suffix={label.toLowerCase()} />;
}

function Row({ icon, color, text }: { icon: 'star' | 'checkmark-circle' | 'eye'; color: string; text: string }) {
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 10 }}>
      <Icon name={icon} size={20} color={color} />
      <Text>{text}</Text>
    </View>
  );
}
