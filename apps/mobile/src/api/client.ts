import type { z } from 'zod';

import { API_BASE } from '@/lib/config';
import { useAuthStore } from '@/state/auth-store';

import { authResponseSchema } from './schemas';

const REQUEST_TIMEOUT_MS = 15_000;
const UPLOAD_TIMEOUT_MS = 45_000;

/** An error the UI can show as-is: `message` is already user-facing Spanish. */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly fieldErrors: Record<string, string[]> = {},
  ) {
    super(message);
    this.name = 'ApiError';
  }

  get isNetwork() {
    return this.status === 0;
  }
}

type RequestOptions<T> = {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  body?: unknown;
  schema?: z.ZodType<T>;
  /** false for login/register/refresh: no bearer token and no refresh-retry. */
  auth?: boolean;
  signal?: AbortSignal;
  /** Slow-by-nature calls (a model writing a draft) need more than the default; everything else keeps the short one. */
  timeoutMs?: number;
};

let refreshInFlight: Promise<boolean> | null = null;

/** Single-flight: if five requests hit 401 at once, only one refresh call is made. */
function refreshSession(): Promise<boolean> {
  refreshInFlight ??= (async () => {
    const { refreshToken, setSession, signOut } = useAuthStore.getState();
    if (!refreshToken) {
      await signOut();
      return false;
    }
    try {
      const session = await request('/auth/refresh', { method: 'POST', body: { refreshToken }, schema: authResponseSchema, auth: false });
      await setSession(session);
      return true;
    } catch (error) {
      // Only an explicit rejection ends the session; a flaky network must not log the user out.
      if (error instanceof ApiError && !error.isNetwork) await signOut();
      return false;
    }
  })().finally(() => {
    refreshInFlight = null;
  });
  return refreshInFlight;
}

export async function request<T = void>(path: string, options: RequestOptions<T> = {}): Promise<T> {
  const { method = 'GET', body, schema, auth = true, signal, timeoutMs } = options;

  const send = async (): Promise<Response> => {
    const controller = new AbortController();
    const isForm = typeof FormData !== 'undefined' && body instanceof FormData;
    const timer = setTimeout(() => controller.abort(), timeoutMs ?? (isForm ? UPLOAD_TIMEOUT_MS : REQUEST_TIMEOUT_MS));
    signal?.addEventListener('abort', () => controller.abort(), { once: true });

    const headers: Record<string, string> = { Accept: 'application/json' };
    // FormData sets its own multipart boundary header.
    if (body !== undefined && !isForm) headers['Content-Type'] = 'application/json';
    const token = useAuthStore.getState().accessToken;
    if (auth && token) headers.Authorization = `Bearer ${token}`;

    try {
      return await fetch(`${API_BASE}${path}`, {
        method,
        headers,
        body: body === undefined ? undefined : isForm ? (body as FormData) : JSON.stringify(body),
        signal: controller.signal,
      });
    } catch (error) {
      if (signal?.aborted) throw error;
      if (__DEV__) console.warn(`Network failure on ${method} ${path}`, error);
      throw new ApiError(
        'No pudimos conectar con el servidor. Revisa tu conexión e inténtalo de nuevo.' + (__DEV__ ? ` [${String(error)}]` : ''),
        0,
      );
    } finally {
      clearTimeout(timer);
    }
  };

  let response = await send();
  if (response.status === 401 && auth && (await refreshSession())) response = await send();

  if (!response.ok) throw await toApiError(response);
  if (response.status === 204) return undefined as T;

  const json: unknown = await response.json();
  if (!schema) return json as T;

  const parsed = schema.safeParse(json);
  if (!parsed.success) {
    if (__DEV__) console.warn(`Unexpected response shape from ${method} ${path}`, parsed.error.issues);
    throw new ApiError('Recibimos una respuesta inesperada del servidor.', response.status);
  }
  return parsed.data;
}

async function toApiError(response: Response): Promise<ApiError> {
  let problem: { title?: string; detail?: string; errors?: Record<string, string[]> } = {};
  try {
    problem = (await response.json()) ?? {};
  } catch {
    // Non-JSON error body: fall through to the generic message.
  }

  const fieldErrors = problem.errors ?? {};
  const firstFieldError = Object.values(fieldErrors)[0]?.[0];
  const fallback =
    response.status === 401 ? 'Tu sesión expiró. Inicia sesión de nuevo.'
    : response.status === 404 ? 'No encontramos lo que buscabas. Puede que ya no esté disponible.'
    : response.status === 429 ? 'Demasiados intentos. Espera un momento e inténtalo otra vez.'
    : response.status >= 500 ? 'El servidor tuvo un problema. Inténtalo en unos minutos.'
    : 'No pudimos completar la acción.';

  // Our own errors always carry a Spanish `detail`. A bare `title` is the framework default ("Not Found"), in English:
  // never show it to the user.
  return new ApiError(firstFieldError ?? problem.detail ?? fallback, response.status, fieldErrors);
}
