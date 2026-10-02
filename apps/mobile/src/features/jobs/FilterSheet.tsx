import { useState } from 'react';
import { View } from 'react-native';

import { useDistricts } from '@/api/queries';
import type { FeedFilters } from '@/api/endpoints';
import type { MatchCategory, WorkModality } from '@/api/schemas';
import { modalityLabel, sortLabel } from '@/lib/format';
import { BottomSheet } from '@/ui/BottomSheet';
import { Button } from '@/ui/Button';
import { Chip } from '@/ui/Chip';
import { Select } from '@/ui/Select';
import { Text } from '@/ui/Text';

import { categoryStyle } from './MatchBadge';

type FilterSheetProps = {
  visible: boolean;
  value: FeedFilters;
  onClose: () => void;
  onApply: (filters: FeedFilters) => void;
};

const SALARY_OPTIONS = [1500, 1800, 2000, 2500, 3000].map((v) => ({ value: v, label: `Desde S/ ${v.toLocaleString('en-US')}` }));

const DATE_OPTIONS = [
  { value: 1, label: 'Hoy' },
  { value: 3, label: 'Últimos 3 días' },
  { value: 7, label: 'Última semana' },
  { value: 30, label: 'Último mes' },
];

const SORTS = ['recent', 'salary'] as const;

const MODALITIES: WorkModality[] = ['onSite', 'hybrid', 'remote'];

// "Poco compatible" is never in the feed, so it is not offered as a filter.
const CATEGORIES: MatchCategory[] = ['excellent', 'veryCompatible', 'compatible', 'review'];

export function FilterSheet({ visible, value, onClose, onApply }: FilterSheetProps) {
  const [draft, setDraft] = useState<FeedFilters>(value);
  const districts = useDistricts();

  // Re-seed the draft each time the sheet opens, so "Cancelar" truly discards edits.
  const [wasVisible, setWasVisible] = useState(visible);
  if (visible !== wasVisible) {
    setWasVisible(visible);
    if (visible) setDraft(value);
  }

  const set = <K extends keyof FeedFilters>(key: K, v: FeedFilters[K] | null) => setDraft((d) => ({ ...d, [key]: v ?? undefined }));

  return (
    <BottomSheet
      visible={visible}
      onClose={onClose}
      title="Filtros"
      footer={
        <View style={{ flexDirection: 'row', gap: 10 }}>
          <Button label="Limpiar" variant="secondary" onPress={() => { onApply({ q: value.q }); onClose(); }} style={{ flex: 1 }} testID="filters-clear" />
          <Button label="Aplicar" onPress={() => { onApply(draft); onClose(); }} style={{ flex: 2 }} testID="filters-apply" />
        </View>
      }
    >
      <View style={{ gap: 8 }}>
        <Text variant="caption" tone="muted">Ordenar por</Text>
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
          <Chip label="Recomendado" icon="sparkles-outline" selected={draft.sort === undefined} onPress={() => set('sort', null)} testID="sort-default" />
          {SORTS.map((s) => (
            <Chip key={s} label={sortLabel[s]} selected={draft.sort === s} onPress={() => set('sort', draft.sort === s ? null : s)} testID={`sort-${s}`} />
          ))}
        </View>
      </View>

      <View style={{ gap: 8 }}>
        <Text variant="caption" tone="muted">Compatibilidad</Text>
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
          {CATEGORIES.map((c) => (
            <Chip key={c} label={categoryStyle[c].label} icon={categoryStyle[c].icon} selected={draft.category === c} onPress={() => set('category', draft.category === c ? null : c)} testID={`filter-${c}`} />
          ))}
        </View>
      </View>

      <View style={{ gap: 8 }}>
        <Text variant="caption" tone="muted">Modalidad</Text>
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
          {MODALITIES.map((m) => (
            <Chip key={m} label={modalityLabel[m]} selected={draft.modality === m} onPress={() => set('modality', draft.modality === m ? null : m)} />
          ))}
        </View>
      </View>

      <Select
        label="Distrito"
        placeholder="Cualquier distrito"
        options={(districts.data ?? []).map((d) => ({ value: d, label: d }))}
        value={draft.district ?? null}
        onChange={(v) => set('district', v)}
        clearLabel="Cualquier distrito"
      />

      <Select label="Sueldo" placeholder="Cualquier sueldo" options={SALARY_OPTIONS} value={draft.minSalary ?? null} onChange={(v) => set('minSalary', v)} clearLabel="Cualquier sueldo" />

      <Select label="Publicado" placeholder="En cualquier momento" options={DATE_OPTIONS} value={draft.postedWithinDays ?? null} onChange={(v) => set('postedWithinDays', v)} clearLabel="En cualquier momento" />
    </BottomSheet>
  );
}

/** Number of active filters (the search text is not counted: it has its own box). */
export function countFilters(f: FeedFilters): number {
  return [f.district, f.modality, f.minSalary, f.postedWithinDays, f.category, f.sort].filter((x) => x !== undefined).length;
}
