import type { ReactNode } from 'react';
import type { Control } from 'react-hook-form';
import { View } from 'react-native';

import type { PreferencesInput } from '@/api/endpoints';
import { useDistricts } from '@/api/queries';
import type { EducationLevel, NotificationFrequency, Preferences, WorkModality } from '@/api/schemas';
import { frequencyLabel, modalityLabel } from '@/lib/format';
import { Chip } from '@/ui/Chip';
import { FormInput } from '@/ui/Input';
import { MultiSelect, Select } from '@/ui/Select';
import { TagInput, ToggleRow } from '@/ui/TagInput';
import { Text } from '@/ui/Text';

import type { PreferencesForm } from './schemas';

const MODALITIES: WorkModality[] = ['onSite', 'hybrid', 'remote'];

const COMMUTE_OPTIONS = [30, 45, 60, 90, 120].map((v) => ({
  value: v,
  label: v === 60 ? '1 hora' : v === 90 ? '1 hora y media' : v === 120 ? '2 horas' : `${v} minutos`,
}));

const EDUCATION_CEILING: { value: EducationLevel; label: string; description: string }[] = [
  { value: 'technical', label: 'Hasta estudios técnicos', description: 'No mostrar ofertas que exijan universidad terminada' },
  { value: 'secondary', label: 'Solo secundaria', description: 'No mostrar ofertas que exijan estudios superiores terminados' },
];

const FREQUENCIES = (Object.keys(frequencyLabel) as NotificationFrequency[]).map((v) => ({ value: v, label: frequencyLabel[v] }));

/** The list/enum part of the preferences (the salary is a text field handled by react-hook-form). */
export type PreferencesDraft = Pick<
  PreferencesInput,
  | 'preferredRoles' | 'excludedRoles' | 'excludedKeywords' | 'preferredModalities' | 'preferredDistricts' | 'excludedDistricts'
  | 'homeDistrict' | 'maxCommuteMinutes' | 'weekdaysOnly' | 'maxRequiredEducation' | 'notificationFrequency' | 'pushEnabled'
>;

export function draftFromPreferences(p: Preferences): PreferencesDraft {
  return {
    preferredRoles: p.preferredRoles, excludedRoles: p.excludedRoles, excludedKeywords: p.excludedKeywords,
    preferredModalities: p.preferredModalities, preferredDistricts: p.preferredDistricts, excludedDistricts: p.excludedDistricts,
    homeDistrict: p.homeDistrict, maxCommuteMinutes: p.maxCommuteMinutes, weekdaysOnly: p.weekdaysOnly,
    maxRequiredEducation: p.maxRequiredEducation, notificationFrequency: p.notificationFrequency, pushEnabled: p.pushEnabled,
  };
}

type Props = {
  control: Control<PreferencesForm>;
  draft: PreferencesDraft;
  onChange: <K extends keyof PreferencesDraft>(key: K, value: PreferencesDraft[K]) => void;
  /** Onboarding edits the roles in the previous step, so it hides them here. */
  showRoles?: boolean;
  showAlerts?: boolean;
};

/** Shared by the onboarding step and the "Preferencias" screen so both stay identical. */
export function PreferencesFields({ control, draft, onChange, showRoles = true, showAlerts = true }: Props) {
  const districts = useDistricts();
  const districtOptions = (districts.data ?? []).map((d) => ({ value: d, label: d }));

  return (
    <>
      <Section title="Sueldo">
        <FormInput control={control} name="minSalary" label="Sueldo mínimo mensual" icon="cash-outline" keyboardType="number-pad" placeholder="1800" suffix="S/" hint="Ocultaremos ofertas que paguen menos que esto." />
      </Section>

      {showRoles ? (
        <Section title="Cargos">
          <TagInput label="Cargos que busco" values={draft.preferredRoles} onChange={(v) => onChange('preferredRoles', v)} placeholder="Ej.: Asistente administrativo" />
          <TagInput label="Cargos que no quiero" values={draft.excludedRoles} onChange={(v) => onChange('excludedRoles', v)} placeholder="Ej.: Cajera" />
          <TagInput label="Palabras que no quiero ver" values={draft.excludedKeywords} onChange={(v) => onChange('excludedKeywords', v)} placeholder="Ej.: call center" hint="Se buscan en el título y la descripción." />
        </Section>
      ) : (
        <Section title="Lo que no quiero ver">
          <TagInput label="Palabras que no quiero ver" values={draft.excludedKeywords} onChange={(v) => onChange('excludedKeywords', v)} placeholder="Ej.: call center" hint="Se buscan en el título y la descripción." />
        </Section>
      )}

      <Section title="Ubicación">
        <Select label="Distrito donde vivo" options={districtOptions} value={draft.homeDistrict} onChange={(v) => onChange('homeDistrict', v)} clearLabel="No indicar" placeholder="Selecciona tu distrito" />
        <Select label="Tiempo máximo de viaje" options={COMMUTE_OPTIONS} value={draft.maxCommuteMinutes} onChange={(v) => onChange('maxCommuteMinutes', v)} clearLabel="Sin límite" placeholder="Sin límite" />
        <MultiSelect label="Distritos preferidos" options={districtOptions} value={draft.preferredDistricts} onChange={(v) => onChange('preferredDistricts', v)} placeholder="Todos" />
        <MultiSelect label="Distritos que no quiero" options={districtOptions} value={draft.excludedDistricts} onChange={(v) => onChange('excludedDistricts', v)} placeholder="Ninguno" />
      </Section>

      <Section title="Cómo quiero trabajar">
        <View style={{ gap: 8 }}>
          <Text variant="caption" tone="muted">Modalidad (si no eliges, te mostramos todas)</Text>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
            {MODALITIES.map((m) => (
              <Chip
                key={m}
                label={modalityLabel[m]}
                selected={draft.preferredModalities.includes(m)}
                onPress={() => onChange('preferredModalities', draft.preferredModalities.includes(m) ? draft.preferredModalities.filter((x) => x !== m) : [...draft.preferredModalities, m])}
              />
            ))}
          </View>
        </View>
        <ToggleRow label="Solo de lunes a viernes" description="Ocultamos ofertas que incluyen sábados o domingos." value={draft.weekdaysOnly} onChange={(v) => onChange('weekdaysOnly', v)} />
        <Select label="Estudios que puedo acreditar" options={EDUCATION_CEILING} value={draft.maxRequiredEducation} onChange={(v) => onChange('maxRequiredEducation', v)} clearLabel="Sin límite" placeholder="Sin límite" />
      </Section>

      {showAlerts ? (
        <Section title="Alertas">
          <Select label="Frecuencia de avisos" options={FREQUENCIES} value={draft.notificationFrequency} onChange={(v) => v && onChange('notificationFrequency', v)} />
          <ToggleRow label="Avisos en el celular" description="Solo te avisamos cuando aparece algo que encaja muy bien contigo. Máximo unos pocos al día y nunca de noche." value={draft.pushEnabled} onChange={(v) => onChange('pushEnabled', v)} />
        </Section>
      ) : null}
    </>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <View style={{ gap: 14 }}>
      <Text variant="heading">{title}</Text>
      {children}
    </View>
  );
}
