import type { Preferences, Profile } from '@/api/schemas';
import { combineDayTime } from '@/features/applications/InterviewPicker';
import { passwordChecks } from '@/features/auth/schemas';
import {
  formatDateTime,
  formatDayLabel,
  greeting,
  initials,
  nextApplicationStage,
  nextInterview,
  pickIndex,
  plural,
} from '@/lib/format';
import { jobsLink } from '@/features/jobs/links';
import { profileStrength } from '@/lib/profile-strength';
import { useToast } from '@/state/toast-store';
import { resolveScheme } from '@/ui/theme';

const profile: Profile = {
  fullName: 'Ana Prueba', email: 'a@b.com', headline: null, experienceMonths: 0, educationLevel: null, educationStatus: null,
  skills: [], languages: [], experience: [], education: [], certifications: [], onboardingCompleted: true, updatedAt: '2026-10-01T00:00:00Z',
};

const prefs: Preferences = {
  minSalary: null, maxSalary: null, preferredRoles: [], excludedRoles: [], excludedKeywords: [], preferredIndustries: [],
  preferredModalities: [], employmentTypes: [], preferredDistricts: [], excludedDistricts: [], homeDistrict: null,
  maxCommuteMinutes: null, weekdaysOnly: false, maxRequiredEducation: null, notificationFrequency: 'daily', pushEnabled: false,
  updatedAt: '2026-10-01T00:00:00Z',
};

describe('avatars', () => {
  it('builds initials from companies and people', () => {
    expect(initials('Importadora Pacífico')).toBe('IP');
    expect(initials('Areli')).toBe('AR');
    expect(initials('  ')).toBe('?');
    expect(initials('Clínica & Santa Aurora')).toBe('CS');
  });

  it('gives the same name the same colour, always inside the palette', () => {
    expect(pickIndex('Importadora Pacífico', 7)).toBe(pickIndex('Importadora Pacífico', 7));
    for (const name of ['a', 'Colegio Los Pinos', 'Ñandú SAC', '']) {
      const i = pickIndex(name, 7);
      expect(i).toBeGreaterThanOrEqual(0);
      expect(i).toBeLessThan(7);
    }
  });
});

describe('dates for humans', () => {
  it('greets by time of day', () => {
    expect(greeting(new Date(2026, 9, 1, 8))).toBe('Buenos días');
    expect(greeting(new Date(2026, 9, 1, 15))).toBe('Buenas tardes');
    expect(greeting(new Date(2026, 9, 1, 22))).toBe('Buenas noches');
    expect(greeting(new Date(2026, 9, 1, 3))).toBe('Buenas noches');
  });

  it('names days relative to today', () => {
    const now = new Date(2026, 9, 1, 12); // jueves 1 oct
    expect(formatDayLabel(new Date(2026, 9, 1, 9), now)).toBe('Hoy');
    expect(formatDayLabel(new Date(2026, 9, 2, 9), now)).toBe('Mañana');
    expect(formatDayLabel(new Date(2026, 8, 30, 9), now)).toBe('Ayer');
    expect(formatDayLabel(new Date(2026, 9, 3, 9), now)).toBe('sáb 3 oct');
  });

  it('formats interview date and time in 24 h', () => {
    const now = new Date(2026, 9, 1, 12);
    expect(formatDateTime(new Date(2026, 9, 2, 9, 5).toISOString(), now)).toBe('Mañana, 09:05');
    expect(formatDateTime(null, now)).toBe('');
    expect(formatDateTime('not a date', now)).toBe('');
  });

  it('combines a picked day and slot into a local date', () => {
    const d = combineDayTime(new Date(2026, 9, 3), '15:30');
    expect([d.getFullYear(), d.getMonth(), d.getDate(), d.getHours(), d.getMinutes()]).toEqual([2026, 9, 3, 15, 30]);
  });
});

describe('tracker', () => {
  it('walks the pipeline one stage at a time and stops at the end', () => {
    expect(nextApplicationStage.found).toBe('interested');
    expect(nextApplicationStage.interested).toBe('applied');
    expect(nextApplicationStage.applied).toBe('interview');
    expect(nextApplicationStage.interview).toBe('offer');
    expect(nextApplicationStage.offer).toBeNull();
    expect(nextApplicationStage.discarded).toBeNull();
  });

  it('finds the closest upcoming interview, with an hour of grace', () => {
    const now = new Date('2026-10-01T12:00:00Z');
    const apps = [
      { id: 'past', status: 'interview' as const, interviewDate: '2026-09-30T12:00:00Z' },
      { id: 'later', status: 'interview' as const, interviewDate: '2026-10-05T12:00:00Z' },
      { id: 'soon', status: 'interview' as const, interviewDate: '2026-10-01T11:30:00Z' },
      { id: 'applied', status: 'applied' as const, interviewDate: '2026-10-01T13:00:00Z' },
      { id: 'nodate', status: 'interview' as const, interviewDate: null },
    ];
    expect(nextInterview(apps, now)?.id).toBe('soon');
    expect(nextInterview([], now)).toBeNull();
  });

  it('pluralises counts', () => {
    expect(plural(1, 'posible', 'posibles')).toBe('1 posible');
    expect(plural(3, 'posible', 'posibles')).toBe('3 posibles');
  });
});

describe('profile strength', () => {
  it('starts low and points to the first missing step', () => {
    const s = profileStrength(profile, prefs, false);
    expect(s.percent).toBe(0);
    expect(s.next?.key).toBe('cv');
    expect(s.label).toBe('Recién empiezas');
  });

  it('reaches 100% only when everything the matcher uses is there', () => {
    const full = profileStrength(
      { ...profile, experienceMonths: 12, educationLevel: 'technical', skills: [1, 2, 3].map((i) => ({ key: `k${i}`, name: `S${i}`, level: 'basic' as const })) },
      { ...prefs, preferredRoles: ['Asistente'], minSalary: 1800, homeDistrict: 'Los Olivos' },
      true,
    );
    expect(full.percent).toBe(100);
    expect(full.next).toBeNull();
    expect(full.label).toBe('Perfil completo');
  });

  it('handles data that is still loading', () => {
    expect(profileStrength(undefined, undefined, false).percent).toBe(0);
  });
});

describe('password checklist', () => {
  it('mirrors the register rules', () => {
    expect(passwordChecks('').map((c) => c.ok)).toEqual([false, false, false]);
    expect(passwordChecks('abc1').map((c) => c.ok)).toEqual([false, true, true]);
    expect(passwordChecks('abcdefg1').every((c) => c.ok)).toBe(true);
  });
});

describe('theme', () => {
  it('follows the phone unless the user picked a scheme', () => {
    expect(resolveScheme('system', 'dark')).toBe('dark');
    expect(resolveScheme('system', 'light')).toBe('light');
    expect(resolveScheme('system', null)).toBe('light');
    expect(resolveScheme('light', 'dark')).toBe('light');
    expect(resolveScheme('dark', 'light')).toBe('dark');
  });
});

describe('toasts', () => {
  afterEach(() => useToast.setState({ current: null }));

  it('keeps one toast at a time and gives actions more time', () => {
    const { show } = useToast.getState();
    show({ message: 'Uno' });
    const first = useToast.getState().current;
    expect(first?.durationMs).toBe(3000);

    show({ message: 'Dos', action: { label: 'Deshacer', onPress: () => undefined } });
    const second = useToast.getState().current;
    expect(second?.message).toBe('Dos');
    expect(second?.durationMs).toBe(5000);

    // A stale timer from the first toast must not close the second one.
    useToast.getState().dismiss(first?.id);
    expect(useToast.getState().current?.message).toBe('Dos');
    useToast.getState().dismiss(second?.id);
    expect(useToast.getState().current).toBeNull();
  });
});

describe('jobs links', () => {
  it('carries the preset and a fresh marker so repeated taps re-apply it', () => {
    expect(jobsLink('new', undefined, 1)).toEqual({ pathname: '/jobs', params: { tab: 'new', at: '1' } });
    expect(jobsLink('forYou', 'review', 2).params).toEqual({ tab: 'forYou', category: 'review', at: '2' });
    expect(jobsLink('new', undefined, 1).params.at).not.toBe(jobsLink('new', undefined, 2).params.at);
  });
});
