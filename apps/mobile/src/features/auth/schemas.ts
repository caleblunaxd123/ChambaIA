import { z } from 'zod';

const email = z.string().trim().min(1, 'Escribe tu correo.').pipe(z.email('Ese correo no parece válido.'));

export const loginSchema = z.object({
  email,
  password: z.string().min(1, 'Escribe tu contraseña.'),
});
export type LoginForm = z.infer<typeof loginSchema>;

export const registerSchema = z.object({
  fullName: z.string().trim().min(2, 'Cuéntanos tu nombre.').max(100, 'Es demasiado largo.'),
  email,
  password: z
    .string()
    .min(8, 'Usa al menos 8 caracteres.')
    .regex(/[a-z]/, 'Incluye al menos una letra minúscula.')
    .regex(/\d/, 'Incluye al menos un número.'),
});
export type RegisterForm = z.infer<typeof registerSchema>;

/** Live checklist under the password field (mirrors the schema rules above). */
export function passwordChecks(password: string): { label: string; ok: boolean }[] {
  return [
    { label: '8 caracteres o más', ok: password.length >= 8 },
    { label: 'Una minúscula', ok: /[a-z]/.test(password) },
    { label: 'Un número', ok: /\d/.test(password) },
  ];
}
