import { ApiError, request } from '@/api/client';
import { useAuthStore } from '@/state/auth-store';

jest.mock('@/state/session-storage', () => ({
  sessionStorage: { load: jest.fn(async () => null), save: jest.fn(async () => undefined), clear: jest.fn(async () => undefined) },
}));

const user = { id: 'u1', email: 'a@b.com', fullName: 'Ana', plan: 'Free' };
const session = (access: string, refresh: string) => ({ accessToken: access, accessTokenExpiresAt: '2030-01-01T00:00:00Z', refreshToken: refresh, user });

const json = (status: number, body?: unknown) => ({ ok: status >= 200 && status < 300, status, json: async () => body }) as Response;

describe('api client', () => {
  const fetchMock = jest.fn();

  beforeEach(() => {
    fetchMock.mockReset();
    globalThis.fetch = fetchMock as unknown as typeof fetch;
    useAuthStore.setState({ status: 'signedIn', user, accessToken: 'old-access', refreshToken: 'old-refresh' });
  });

  it('sends the bearer token', async () => {
    fetchMock.mockResolvedValueOnce(json(200, { ok: true }));
    await request('/profile');
    expect(fetchMock.mock.calls[0][1].headers.Authorization).toBe('Bearer old-access');
  });

  it('refreshes once on 401 and retries with the new token', async () => {
    fetchMock
      .mockResolvedValueOnce(json(401))
      .mockResolvedValueOnce(json(200, session('new-access', 'new-refresh')))
      .mockResolvedValueOnce(json(200, { hello: 'world' }));

    const result = await request<{ hello: string }>('/profile');

    expect(result.hello).toBe('world');
    expect(useAuthStore.getState().accessToken).toBe('new-access');
    expect(fetchMock.mock.calls[2][1].headers.Authorization).toBe('Bearer new-access');
  });

  it('shares a single refresh between concurrent 401s', async () => {
    let refreshCalls = 0;
    fetchMock.mockImplementation(async (url: string, init: { headers: Record<string, string> }) => {
      if (url.endsWith('/auth/refresh')) {
        refreshCalls += 1;
        return json(200, session('new-access', 'new-refresh'));
      }
      return init.headers.Authorization === 'Bearer new-access' ? json(200, {}) : json(401);
    });

    await Promise.all([request('/a'), request('/b'), request('/c')]);

    expect(refreshCalls).toBe(1);
  });

  it('signs the user out when the refresh token is rejected', async () => {
    fetchMock.mockResolvedValueOnce(json(401)).mockResolvedValueOnce(json(401, { detail: 'La sesión expiró.' }));

    await expect(request('/profile')).rejects.toBeInstanceOf(ApiError);
    expect(useAuthStore.getState().status).toBe('signedOut');
  });

  it('keeps the session when the network is down during refresh', async () => {
    fetchMock.mockResolvedValueOnce(json(401)).mockRejectedValueOnce(new Error('offline'));

    await expect(request('/profile')).rejects.toBeInstanceOf(ApiError);
    expect(useAuthStore.getState().status).toBe('signedIn');
  });

  it('surfaces the API problem details as a user-facing message', async () => {
    fetchMock.mockResolvedValueOnce(json(409, { title: 'Correo en uso', detail: 'Ya existe una cuenta con ese correo.' }));

    await expect(request('/auth/register', { method: 'POST', body: {}, auth: false })).rejects.toMatchObject({
      message: 'Ya existe una cuenta con ese correo.',
      status: 409,
    });
  });

  it('never shows the framework default title in English', async () => {
    fetchMock.mockResolvedValueOnce(json(404, { title: 'Not Found', status: 404 }));

    await expect(request('/matches/x/reset', { method: 'POST' })).rejects.toMatchObject({
      message: 'No encontramos lo que buscabas. Puede que ya no esté disponible.',
      status: 404,
    });
  });

  it('turns network failures into a friendly error', async () => {
    fetchMock.mockRejectedValueOnce(new TypeError('Network request failed'));

    const error = await request('/jobs').catch((e: ApiError) => e);

    expect((error as ApiError).isNetwork).toBe(true);
    expect((error as ApiError).message).toMatch(/conectar/);
  });
});
