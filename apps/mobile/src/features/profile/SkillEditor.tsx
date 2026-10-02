import { useMemo, useState } from 'react';
import { Pressable, View } from 'react-native';

import { useSkillCatalog } from '@/api/queries';
import type { Skill, SkillLevel } from '@/api/schemas';
import { skillLevelLabel } from '@/lib/format';
import { haptics } from '@/lib/haptics';
import { Card } from '@/ui/Card';
import { Chip } from '@/ui/Chip';
import { Icon } from '@/ui/Icon';
import { Input } from '@/ui/Input';
import { Text } from '@/ui/Text';
import { useTheme } from '@/ui/theme';

const LEVELS: SkillLevel[] = ['basic', 'intermediate', 'advanced'];

/** Normalises text the way the backend does closely enough for suggestions (the server stays the authority). */
const fold = (s: string) => s.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase().trim();

type Props = {
  skills: Skill[];
  onChange: (skills: Skill[]) => void;
};

export function SkillEditor({ skills, onChange }: Props) {
  const { colors, radius } = useTheme();
  const catalog = useSkillCatalog();
  const [draft, setDraft] = useState('');

  const suggestions = useMemo(() => {
    const q = fold(draft);
    const have = new Set(skills.map((s) => s.key));
    const all = catalog.data ?? [];
    return (q.length === 0 ? all : all.filter((c) => fold(c.name).includes(q))).filter((c) => !have.has(c.key)).slice(0, 8);
  }, [catalog.data, draft, skills]);

  const add = (name: string, key = '') => {
    const clean = name.trim();
    if (!clean || skills.some((s) => fold(s.name) === fold(clean))) return;
    haptics.tap();
    onChange([...skills, { key, name: clean, level: 'intermediate' }]);
    setDraft('');
  };

  const setLevel = (index: number, level: SkillLevel) => onChange(skills.map((s, i) => (i === index ? { ...s, level } : s)));

  return (
    <View style={{ gap: 12 }}>
      <Input label="Agregar una habilidad" value={draft} onChangeText={setDraft} placeholder="Ej.: Excel, atención al cliente…" hint="Toca una sugerencia o escribe y presiona enter." onSubmitEditing={() => add(draft)} returnKeyType="done" icon="add-circle-outline" />

      {suggestions.length > 0 ? (
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
          {suggestions.map((s) => (
            <Chip key={s.key} label={`+ ${s.name}`} onPress={() => add(s.name, s.key)} />
          ))}
        </View>
      ) : null}

      <View style={{ gap: 8 }}>
        {skills.map((skill, index) => (
          <Card key={`${skill.key}-${skill.name}`} padded={false} style={{ padding: 12, gap: 10 }}>
            <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
              <Text variant="bodyStrong" style={{ flex: 1 }}>{skill.name}</Text>
              <Pressable accessibilityLabel={`Quitar ${skill.name}`} hitSlop={10} onPress={() => onChange(skills.filter((_, i) => i !== index))}>
                <Icon name="trash-outline" size={19} tone="subtle" />
              </Pressable>
            </View>
            <View style={{ flexDirection: 'row', gap: 6 }}>
              {LEVELS.map((level) => {
                const active = skill.level === level;
                return (
                  <Pressable
                    key={level}
                    accessibilityRole="button"
                    accessibilityState={{ selected: active }}
                    onPress={() => setLevel(index, level)}
                    style={{ flex: 1, alignItems: 'center', paddingVertical: 7, borderRadius: radius.pill, backgroundColor: active ? colors.primaryTint : colors.surfaceMuted, borderWidth: 1, borderColor: active ? colors.primary : 'transparent' }}
                  >
                    <Text variant="caption" tone={active ? 'primary' : 'muted'}>{skillLevelLabel[level]}</Text>
                  </Pressable>
                );
              })}
            </View>
          </Card>
        ))}
      </View>
    </View>
  );
}
