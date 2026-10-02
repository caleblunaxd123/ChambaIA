import { buildQuery } from '@/api/endpoints';
import { jobDetailResponseSchema, matchDetailSchema } from '@/api/schemas';
import { dimensionLevelLabel, sortLabel } from '@/lib/format';

const match = {
  category: 'veryCompatible',
  categoryLabel: 'Muy compatible',
  recommendation: 'La oferta cumple la mayoría de tus criterios.',
  status: 'seen',
  matchedSkills: ['Atención al cliente'],
  missingSkills: [],
  reasons: [{ code: 'role-match', title: 'Coincide con un cargo que buscas', detail: 'Asistente Administrativo' }],
  warnings: [],
  dimensions: [
    { key: 'role', label: 'Cargo', level: 'strong', note: 'Coincide con «Asistente Administrativo», un cargo que buscas.' },
    { key: 'skills', label: 'Habilidades', level: 'medium', note: 'Tienes 3 de 4 de las que piden.' },
    { key: 'meaning', label: 'Parecido con tu perfil', level: 'weak', note: 'El contenido de la oferta es distinto a lo que has hecho.' },
  ],
  improvements: [
    { title: 'Sube «Excel» a nivel intermedio en tu perfil', detail: 'Solo si ya lo sabes hacer.', resultCategory: 'excellent', resultLabel: 'Excelente opción' },
  ],
};

describe('match explanation contract', () => {
  it('parses the per-axis levels and the what-if suggestions', () => {
    const parsed = matchDetailSchema.parse(match);

    expect(parsed.dimensions.map((d) => d.level)).toEqual(['strong', 'medium', 'weak']);
    expect(parsed.improvements[0].resultCategory).toBe('excellent');
  });

  it('rejects an unknown level so a backend change fails loudly in one place', () => {
    const broken = { ...match, dimensions: [{ key: 'role', label: 'Cargo', level: 'great', note: '' }] };
    expect(matchDetailSchema.safeParse(broken).success).toBe(false);
  });

  it('still requires the explanation fields on the detail response', () => {
    const { dimensions: _d, improvements: _i, ...legacy } = match;
    expect(matchDetailSchema.safeParse(legacy).success).toBe(false);
    expect(jobDetailResponseSchema.shape.match.safeParse(null).success).toBe(true);
  });
});

describe('feed sorting', () => {
  it('sends the chosen sort and omits it when the tab default applies', () => {
    expect(buildQuery({ tab: 'forYou', sort: 'salary', page: 1 })).toBe('?tab=forYou&sort=salary&page=1');
    expect(buildQuery({ tab: 'forYou', sort: undefined, page: 1 })).toBe('?tab=forYou&page=1');
  });

  it('has Spanish labels for every level and sort', () => {
    expect(dimensionLevelLabel).toEqual({ strong: 'Alto', medium: 'Medio', weak: 'Bajo' });
    expect(sortLabel.recent).toBe('Más recientes');
    expect(sortLabel.salary).toBe('Mejor sueldo');
  });
});
