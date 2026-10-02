import { notificationSchema, notificationSummarySchema, testNotificationSchema } from '@/api/schemas';
import {
  type PushEnvironment,
  describePushStatus,
  notificationTarget,
  permissionState,
  pushAvailability,
  testResultMessage,
  unreadBadge,
} from '@/features/notifications/push';

const phone: PushEnvironment = { os: 'android', isDevice: true, isExpoGo: false, projectId: 'abc-123' };

describe('push availability', () => {
  it('is available on a real phone with a development build and an EAS project', () => {
    expect(pushAvailability(phone)).toEqual({ ok: true, projectId: 'abc-123' });
    expect(pushAvailability({ ...phone, os: 'ios', isExpoGo: true })).toEqual({ ok: true, projectId: 'abc-123' }); // Expo Go still gets pushes on iOS
  });

  it.each([
    [{ os: 'web' }, 'web'],
    [{ isDevice: false }, 'simulator'],
    [{ isExpoGo: true }, 'expo-go'],
    [{ projectId: undefined }, 'no-project'],
  ] as const)('explains why it is not available (%j)', (override, reason) => {
    const result = pushAvailability({ ...phone, ...override });
    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.reason).toBe(reason);
      expect(result.message.length).toBeGreaterThan(20);
    }
  });

  it('checks the cheapest blockers first (web before everything else)', () => {
    const result = pushAvailability({ os: 'web', isDevice: false, isExpoGo: true, projectId: undefined });
    expect(result.ok === false && result.reason).toBe('web');
  });
});

describe('permission state', () => {
  it('tells apart granted, askable and blocked', () => {
    expect(permissionState({ granted: true, canAskAgain: true })).toBe('granted');
    expect(permissionState({ granted: false, canAskAgain: true })).toBe('ask');
    expect(permissionState({ granted: false, canAskAgain: false })).toBe('blocked');
  });
});

describe('status wording', () => {
  it('never claims alerts work when they do not', () => {
    expect(describePushStatus({ state: 'active' }, true).tone).toBe('success');
    for (const state of [{ state: 'off' }, { state: 'blocked' }, { state: 'error' }] as const) {
      expect(describePushStatus(state, true).tone).toBe('warning');
    }
    expect(describePushStatus({ state: 'unavailable', reason: 'simulator', message: 'Solo en un teléfono real.' }, true).detail).toBe('Solo en un teléfono real.');
  });

  it('says alerts are off when the user turned them off, whatever the phone state is', () => {
    expect(describePushStatus({ state: 'active' }, false).title).toBe('Avisos desactivados');
  });

  it('never uses pressure language', () => {
    const states = [{ state: 'checking' }, { state: 'off' }, { state: 'blocked' }, { state: 'active' }, { state: 'error' }] as const;
    const all = states.map((s) => describePushStatus(s, true).detail).join(' ');
    expect(all).not.toMatch(/¡|urgente|última oportunidad/i);
  });
});

describe('where a tapped notice goes', () => {
  const id = '3f2b8c1e-9d4a-4c55-8a1b-0e5f6a7b8c9d';

  it('opens the job when the payload carries a well-formed job id', () => {
    expect(notificationTarget({ type: 'new-jobs', jobId: id })).toEqual({ kind: 'job', jobId: id });
    expect(notificationTarget({ jobId: id.toUpperCase() })).toEqual({ kind: 'job', jobId: id.toUpperCase() });
  });

  it('falls back to the inbox for anything else, including hostile payloads', () => {
    expect(notificationTarget({ type: 'test' })).toEqual({ kind: 'inbox' });
    expect(notificationTarget({ jobId: null })).toEqual({ kind: 'inbox' });
    expect(notificationTarget({ jobId: '../../etc/passwd' })).toEqual({ kind: 'inbox' });
    expect(notificationTarget({ jobId: `${id}/../x` })).toEqual({ kind: 'inbox' });
    expect(notificationTarget({ jobId: 42 })).toEqual({ kind: 'inbox' });
    expect(notificationTarget(undefined)).toEqual({ kind: 'inbox' });
    expect(notificationTarget('jobId')).toEqual({ kind: 'inbox' });
  });
});

describe('bell badge', () => {
  it('shows nothing at zero, the number up to nine and 9+ beyond', () => {
    expect(unreadBadge(0)).toBeNull();
    expect(unreadBadge(-3)).toBeNull();
    expect(unreadBadge(Number.NaN)).toBeNull();
    expect(unreadBadge(1)).toBe('1');
    expect(unreadBadge(9)).toBe('9');
    expect(unreadBadge(10)).toBe('9+');
    expect(unreadBadge(250)).toBe('9+');
  });
});

describe('test notification result', () => {
  it('reports honestly what happened', () => {
    expect(testResultMessage({ devices: 1, accepted: 1, pushConfigured: true })).toMatch(/llegar/);
    expect(testResultMessage({ devices: 0, accepted: 0, pushConfigured: true })).toMatch(/no está registrado/);
    expect(testResultMessage({ devices: 1, accepted: 0, pushConfigured: false })).toMatch(/aún no está activo/);
    expect(testResultMessage({ devices: 1, accepted: 0, pushConfigured: true })).toMatch(/no pudimos entregarlo/);
  });
});

describe('API contract', () => {
  it('parses what the backend sends', () => {
    expect(
      notificationSchema.parse({ id: 'a', kind: 'newJobs', title: 't', body: 'b', jobId: null, matchCount: 2, strongCount: 1, createdAt: '2026-10-01T10:00:00Z', readAt: null }).kind,
    ).toBe('newJobs');
    expect(notificationSummarySchema.parse({ unread: 2, activeDevices: 1, pushConfigured: false }).unread).toBe(2);
    expect(testNotificationSchema.parse({ devices: 1, accepted: 0, pushConfigured: false }).accepted).toBe(0);
  });

  it('rejects an unknown notification kind loudly instead of rendering garbage', () => {
    expect(() => notificationSchema.parse({ id: 'a', kind: 'ads', title: 't', body: 'b', jobId: null, matchCount: 0, strongCount: 0, createdAt: 'x', readAt: null })).toThrow();
  });
});
