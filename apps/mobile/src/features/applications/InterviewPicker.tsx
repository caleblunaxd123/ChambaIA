import { ScrollView, View } from 'react-native';

import { formatDayLabel } from '@/lib/format';
import { Chip } from '@/ui/Chip';
import { Text } from '@/ui/Text';

const DAYS_AHEAD = 21;
const TIMES = ['08:00', '09:00', '10:00', '11:00', '12:00', '14:00', '15:00', '16:00', '17:00', '18:00'];

const startOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate());

/** Combines a calendar day and an "HH:mm" slot into a local Date. */
export function combineDayTime(day: Date, time: string): Date {
  const [h, m] = time.split(':').map(Number);
  return new Date(day.getFullYear(), day.getMonth(), day.getDate(), h, m);
}

type Props = {
  value: Date | null;
  onChange: (value: Date) => void;
  /** Removes the date (the card stays in "Entrevista"). */
  onClear?: () => void;
  now?: Date;
};

/**
 * Dependency-free date/time picker for interviews: the next three weeks as chips plus the usual office-hour slots.
 * Covers almost every real case without a native picker module (works in Expo Go and on web).
 */
export function InterviewPicker({ value, onChange, onClear, now = new Date() }: Props) {
  const today = startOfDay(now);
  const days = Array.from({ length: DAYS_AHEAD }, (_, i) => new Date(today.getFullYear(), today.getMonth(), today.getDate() + i));
  // A date already saved in the past (or further away) stays selectable so editing never loses it.
  if (value && !days.some((d) => d.getTime() === startOfDay(value).getTime())) days.unshift(startOfDay(value));

  const selectedDay = value ? startOfDay(value).getTime() : null;
  const selectedTime = value ? `${String(value.getHours()).padStart(2, '0')}:${String(value.getMinutes()).padStart(2, '0')}` : null;
  const times = selectedTime && !TIMES.includes(selectedTime) ? [...TIMES, selectedTime].sort() : TIMES;

  return (
    <View style={{ gap: 10 }} testID="interview-picker">
      <Text variant="caption" tone="muted">Día de la entrevista</Text>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: 8 }} style={{ flexGrow: 0 }}>
        {days.map((d) => (
          <Chip
            key={d.getTime()}
            label={formatDayLabel(d, now)}
            selected={selectedDay === d.getTime()}
            onPress={() => onChange(combineDayTime(d, selectedTime ?? '10:00'))}
          />
        ))}
      </ScrollView>
      {value ? (
        <>
          <Text variant="caption" tone="muted">Hora</Text>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
            {times.map((t) => (
              <Chip key={t} label={t} selected={selectedTime === t} onPress={() => onChange(combineDayTime(value, t))} />
            ))}
          </View>
          {onClear ? (
            <View style={{ flexDirection: 'row' }}>
              <Chip label="Quitar fecha" icon="close" onPress={onClear} testID="interview-clear" />
            </View>
          ) : null}
        </>
      ) : null}
    </View>
  );
}
