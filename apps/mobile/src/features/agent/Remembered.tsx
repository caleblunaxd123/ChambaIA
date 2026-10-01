import { View } from 'react-native';

import type { PreferencesInput } from '@/api/endpoints';
import type { Preferences, WorkModality } from '@/api/schemas';
import { formatMoney, modalityLabel } from '@/lib/format';
import { Chip } from '@/ui/Chip';
import { Text } from '@/ui/Text';

type Props = {
  prefs: Preferences;
  onChange: (next: PreferencesInput) => void;
  disabled?: boolean;
};

const toInput = (p: Preferences): PreferencesInput => {
  const { updatedAt: _ignored, ...rest } = p;
  return rest;
};

/** Everything the agent currently remembers about what the user wants. Each chip can be removed right here. */
export function Remembered({ prefs, onChange, disabled }: Props) {
  const edit = (patch: Partial<PreferencesInput>) => {
    if (!disabled) onChange({ ...toInput(prefs), ...patch });
  };

  const groups: { title: string; chips: { key: string; label: string; icon?: 'location-outline' | 'cash-outline'; onRemove: () => void }[] }[] = [
    {
      title: 'Busco',
      chips: prefs.preferredRoles.map((r) => ({ key: `role-${r}`, label: r, onRemove: () => edit({ preferredRoles: prefs.preferredRoles.filter((x) => x !== r) }) })),
    },
    {
      title: 'No quiero ver',
      chips: [
        ...prefs.excludedDistricts.map((d) => ({ key: `ed-${d}`, label: `Trabajos en ${d}`, onRemove: () => edit({ excludedDistricts: prefs.excludedDistricts.filter((x) => x !== d) }) })),
        ...prefs.excludedKeywords.map((k) => ({ key: `ek-${k}`, label: k, onRemove: () => edit({ excludedKeywords: prefs.excludedKeywords.filter((x) => x !== k) }) })),
        ...prefs.excludedRoles.map((k) => ({ key: `er-${k}`, label: k, onRemove: () => edit({ excludedRoles: prefs.excludedRoles.filter((x) => x !== k) }) })),
      ],
    },
    {
      title: 'Condiciones',
      chips: [
        ...(prefs.minSalary != null ? [{ key: 'salary', label: `Mínimo ${formatMoney(prefs.minSalary)}`, icon: 'cash-outline' as const, onRemove: () => edit({ minSalary: null }) }] : []),
        ...(prefs.maxCommuteMinutes != null ? [{ key: 'commute', label: `Viaje máx. ${prefs.maxCommuteMinutes} min`, onRemove: () => edit({ maxCommuteMinutes: null }) }] : []),
        ...(prefs.homeDistrict ? [{ key: 'home', label: `Vivo en ${prefs.homeDistrict}`, icon: 'location-outline' as const, onRemove: () => edit({ homeDistrict: null }) }] : []),
        ...(prefs.weekdaysOnly ? [{ key: 'weekdays', label: 'Solo lunes a viernes', onRemove: () => edit({ weekdaysOnly: false }) }] : []),
        ...prefs.preferredModalities.map((m: WorkModality) => ({ key: `mod-${m}`, label: modalityLabel[m], onRemove: () => edit({ preferredModalities: prefs.preferredModalities.filter((x) => x !== m) }) })),
        ...prefs.preferredDistricts.map((d) => ({ key: `pd-${d}`, label: `Prefiero ${d}`, onRemove: () => edit({ preferredDistricts: prefs.preferredDistricts.filter((x) => x !== d) }) })),
      ],
    },
  ];

  const visible = groups.filter((g) => g.chips.length > 0);
  if (visible.length === 0) {
    return <Text tone="muted">Todavía no me has dicho qué buscas. Escríbeme abajo o completa tus preferencias en tu perfil.</Text>;
  }

  return (
    <View style={{ gap: 14 }}>
      {visible.map((g) => (
        <View key={g.title} style={{ gap: 8 }}>
          <Text variant="label" tone="subtle">{g.title}</Text>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
            {g.chips.map((c) => (
              <Chip key={c.key} label={c.label} icon={c.icon} onRemove={c.onRemove} />
            ))}
          </View>
        </View>
      ))}
    </View>
  );
}
