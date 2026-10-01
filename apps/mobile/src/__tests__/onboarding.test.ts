import type { DetectedResume, Profile } from '@/api/schemas';
import { applyDetected, draftFromProfile, draftToMonths, draftToProfileInput, formatPeriod, unionText } from '@/features/onboarding/mapping';

const emptyProfile: Profile = {
  fullName: 'Ana Prueba', email: 'a@b.com', headline: null, experienceMonths: 0, educationLevel: null, educationStatus: null,
  skills: [], languages: [], experience: [], education: [], certifications: [], onboardingCompleted: false, updatedAt: '2026-10-01T00:00:00Z',
};

const detected: DetectedResume = {
  fullNameGuess: 'Areli Quispe Mamani',
  headline: 'Asistente administrativa con experiencia en facturación',
  experienceMonths: 55,
  educationLevel: 'university',
  educationStatus: 'inProgress',
  skills: [{ key: 'excel', name: 'Excel', level: 'basic' }],
  languages: [{ name: 'Inglés', level: 'Básico' }],
  experience: [{ title: 'Asistente Administrativa', company: 'Colegio X', startDate: '2023-12-01', endDate: null, description: null }],
  education: [],
  certifications: ['Excel básico - Cibertec'],
  suggestedRoles: ['Asistente Administrativo', 'Back Office'],
  warnings: [],
};

describe('onboarding mapping', () => {
  it('starts from the existing profile and the account name', () => {
    const draft = draftFromProfile(undefined, 'Ana', ['Facturación']);
    expect(draft.fullName).toBe('Ana');
    expect(draft.roles).toEqual(['Facturación']);
    expect(draft.years).toBe('0');
  });

  it('lays the detected data over the draft but never replaces the name the user typed', () => {
    const draft = applyDetected(draftFromProfile(emptyProfile, 'Ana Prueba', ['Facturación']), detected);

    expect(draft.fullName).toBe('Ana Prueba');
    expect(draft.years).toBe('4');
    expect(draft.months).toBe('7');
    expect(draft.educationLevel).toBe('university');
    expect(draft.skills).toHaveLength(1);
    expect(draft.roles).toEqual(['Asistente Administrativo', 'Back Office', 'Facturación']);
  });

  it('uses the detected name only when the user has none', () => {
    expect(applyDetected(draftFromProfile(undefined, '', []), detected).fullName).toBe('Areli Quispe Mamani');
  });

  it('keeps what the user already had when the CV yields nothing for a section', () => {
    const existing = draftFromProfile({ ...emptyProfile, experienceMonths: 18, skills: [{ key: 'word', name: 'Word', level: 'intermediate' }] }, 'Ana');
    const nothing: DetectedResume = { ...detected, experienceMonths: 0, skills: [], headline: null, educationLevel: null, languages: [], certifications: [], experience: [] };

    const draft = applyDetected(existing, nothing);

    expect(draftToMonths(draft)).toBe(18);
    expect(draft.skills.map((s) => s.key)).toEqual(['word']);
  });

  it('builds the profile payload and clears the status when there is no education level', () => {
    const draft = { ...draftFromProfile(emptyProfile, 'Ana'), educationLevel: null, educationStatus: 'completed' as const, headline: '  ', years: '2', months: '9' };

    const input = draftToProfileInput(draft, true);

    expect(input.experienceMonths).toBe(33);
    expect(input.headline).toBeNull();
    expect(input.educationStatus).toBeNull();
    expect(input.completeOnboarding).toBe(true);
    expect(draftToProfileInput(draft).completeOnboarding).toBe(false);
  });

  it('merges role lists ignoring case and blanks', () => {
    expect(unionText(['Back Office', ' '], ['back office', 'Facturación'])).toEqual(['Back Office', 'Facturación']);
  });

  it('formats periods in Spanish', () => {
    expect(formatPeriod('2023-12-01', null)).toBe('dic 2023 – actualidad');
    expect(formatPeriod('2022-04-01', '2023-11-01')).toBe('abr 2022 – nov 2023');
    expect(formatPeriod(null, null)).toBe('');
  });
});
