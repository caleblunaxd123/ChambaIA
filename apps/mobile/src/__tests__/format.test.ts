import { firstName, formatDuration, formatMoney, formatRelativeTime, formatSalary, isFresh } from '@/lib/format';

describe('format', () => {
  it('writes durations in natural Spanish', () => {
    expect(formatDuration(33)).toBe('2 años y 9 meses');
    expect(formatDuration(12)).toBe('1 año');
    expect(formatDuration(1)).toBe('1 mes');
    expect(formatDuration(0)).toBe('Sin experiencia');
  });

  it('formats money and salary ranges', () => {
    expect(formatMoney(1800)).toBe('S/ 1,800');
    expect(formatSalary({ salaryMin: 1900, salaryMax: 2200 })).toBe('S/ 1,900 - S/ 2,200');
    expect(formatSalary({ salaryMin: 1800, salaryMax: 1800 })).toBe('S/ 1,800');
    expect(formatSalary({ salaryMin: 1800, salaryMax: null })).toBe('Desde S/ 1,800');
    expect(formatSalary({ salaryMin: null, salaryMax: null })).toBeNull();
  });

  it('formats relative time', () => {
    const now = new Date('2026-10-01T12:00:00Z');
    expect(formatRelativeTime('2026-10-01T11:45:00Z', now)).toBe('hace 15 min');
    expect(formatRelativeTime('2026-10-01T09:00:00Z', now)).toBe('hace 3 h');
    expect(formatRelativeTime('2026-09-30T10:00:00Z', now)).toBe('ayer');
    expect(formatRelativeTime('2026-09-27T12:00:00Z', now)).toBe('hace 4 días');
    expect(formatRelativeTime(null, now)).toBe('');
  });

  it('flags fresh offers', () => {
    const now = new Date('2026-10-01T12:00:00Z');
    expect(isFresh('2026-10-01T02:00:00Z', now)).toBe(true);
    expect(isFresh('2026-09-29T02:00:00Z', now)).toBe(false);
  });

  it('extracts the first name', () => {
    expect(firstName('Areli Demo')).toBe('Areli');
    expect(firstName(undefined)).toBe('');
  });
});
