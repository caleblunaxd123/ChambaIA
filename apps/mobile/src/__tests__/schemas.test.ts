import { buildQuery } from '@/api/endpoints';
import { loginSchema, registerSchema } from '@/features/auth/schemas';
import { preferencesFormSchema, profileFormSchema } from '@/features/profile/schemas';
import { resolveApiUrl } from '@/lib/config';

describe('auth schemas', () => {
  it('accepts valid credentials and trims the email', () => {
    expect(loginSchema.parse({ email: '  a@b.com ', password: 'x' }).email).toBe('a@b.com');
  });

  it('rejects bad emails and weak passwords with Spanish messages', () => {
    const r = registerSchema.safeParse({ fullName: 'A', email: 'nope', password: 'abc' });
    expect(r.success).toBe(false);
    const messages = r.error!.issues.map((i) => i.message);
    expect(messages).toEqual(expect.arrayContaining(['Cuéntanos tu nombre.', 'Ese correo no parece válido.', 'Usa al menos 8 caracteres.']));
  });

  it('requires a digit in the password', () => {
    expect(registerSchema.safeParse({ fullName: 'Ana', email: 'a@b.com', password: 'sinnumeros' }).success).toBe(false);
    expect(registerSchema.safeParse({ fullName: 'Ana', email: 'a@b.com', password: 'conNumero1' }).success).toBe(true);
  });
});

describe('profile schemas', () => {
  it('accepts numeric experience and rejects text or out-of-range months', () => {
    expect(profileFormSchema.safeParse({ fullName: 'Ana', headline: '', years: '2', months: '9' }).success).toBe(true);
    expect(profileFormSchema.safeParse({ fullName: 'Ana', headline: '', years: 'dos', months: '0' }).success).toBe(false);
    expect(profileFormSchema.safeParse({ fullName: 'Ana', headline: '', years: '1', months: '12' }).success).toBe(false);
  });

  it('only accepts digits for the minimum salary', () => {
    expect(preferencesFormSchema.safeParse({ minSalary: '1800' }).success).toBe(true);
    expect(preferencesFormSchema.safeParse({ minSalary: '' }).success).toBe(true);
    expect(preferencesFormSchema.safeParse({ minSalary: 'S/1800' }).success).toBe(false);
  });
});

describe('api helpers', () => {
  it('builds query strings skipping empty values', () => {
    expect(buildQuery({ tab: 'forYou', q: undefined, district: '', page: 2 })).toBe('?tab=forYou&page=2');
    expect(buildQuery({})).toBe('');
  });

  it('prefers the explicit API url and strips trailing slashes', () => {
    expect(resolveApiUrl('http://10.0.2.2:5180///')).toBe('http://10.0.2.2:5180');
  });
});
