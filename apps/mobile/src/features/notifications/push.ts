import type { NotificationKind } from '@/api/schemas';
import type { IconName } from '@/ui/Icon';

/**
 * Pure rules for push notifications: can this device receive them, where does a tapped notice go, how do we word the
 * state. Kept free of native modules so it is fully unit-tested; the native side lives in push-service.ts.
 */

export type PushEnvironment = {
  os: string;
  /** False on emulators and simulators: they cannot receive real pushes. */
  isDevice: boolean;
  /** Expo Go on Android cannot receive remote pushes since SDK 53 (a development build is required). */
  isExpoGo: boolean;
  /** The EAS project the Expo push token is issued for. */
  projectId: string | undefined;
};

export type UnavailableReason = 'web' | 'simulator' | 'expo-go' | 'no-project';

export type PushAvailability = { ok: true; projectId: string } | { ok: false; reason: UnavailableReason; message: string };

const UNAVAILABLE_MESSAGE: Record<UnavailableReason, string> = {
  web: 'Los avisos al celular funcionan en la app de Android o iPhone, no en el navegador.',
  simulator: 'Los avisos al celular solo llegan a un teléfono real. En el emulador verás los avisos en esta bandeja.',
  'expo-go': 'Expo Go no recibe avisos al celular. Instala la app de ChambaIA (versión de desarrollo) para activarlos.',
  'no-project': 'Falta conectar el servicio de avisos de esta instalación. Mientras tanto verás los avisos aquí, en tu bandeja.',
};

export function pushAvailability(env: PushEnvironment): PushAvailability {
  const fail = (reason: UnavailableReason): PushAvailability => ({ ok: false, reason, message: UNAVAILABLE_MESSAGE[reason] });
  if (env.os !== 'android' && env.os !== 'ios') return fail('web');
  if (!env.isDevice) return fail('simulator');
  if (env.isExpoGo && env.os === 'android') return fail('expo-go');
  if (!env.projectId) return fail('no-project');
  return { ok: true, projectId: env.projectId };
}

export type PushPermission = 'granted' | 'ask' | 'blocked';

/** `canAskAgain` is false once the user denied twice (Android) or denied at all (iOS): only system settings can change it. */
export function permissionState(permission: { granted: boolean; canAskAgain: boolean }): PushPermission {
  if (permission.granted) return 'granted';
  return permission.canAskAgain ? 'ask' : 'blocked';
}

export type PushStatus =
  | { state: 'checking' }
  | { state: 'unavailable'; reason: UnavailableReason; message: string }
  /** Permission not asked (or not granted) yet: the user can turn alerts on. */
  | { state: 'off' }
  | { state: 'blocked' }
  | { state: 'active' }
  | { state: 'error' };

export type StatusCopy = { title: string; detail: string; tone: 'success' | 'warning' | 'muted' };

/** What the notifications screen says about this phone. Honest about every state, never "all good" when it is not. */
export function describePushStatus(status: PushStatus, pushEnabled: boolean): StatusCopy {
  if (!pushEnabled) {
    return { title: 'Avisos desactivados', detail: 'Tu agente no te enviará avisos. Puedes activarlos en «Lo que busco», dentro de tu perfil.', tone: 'muted' };
  }
  switch (status.state) {
    case 'checking':
      return { title: 'Revisando este celular…', detail: 'Un momento.', tone: 'muted' };
    case 'unavailable':
      return { title: 'Avisos solo en tu bandeja', detail: status.message, tone: 'muted' };
    case 'off':
      return { title: 'Activa los avisos en este celular', detail: 'Te avisaremos cuando aparezca algo que encaje muy bien contigo. Nada de spam.', tone: 'warning' };
    case 'blocked':
      return { title: 'Los avisos están bloqueados', detail: 'Permítelos desde los ajustes de tu celular para recibirlos.', tone: 'warning' };
    case 'error':
      return { title: 'No pudimos activar los avisos', detail: 'Revisa tu conexión e inténtalo de nuevo.', tone: 'warning' };
    case 'active':
      return { title: 'Avisos activados en este celular', detail: 'Solo te avisamos cuando algo encaja muy bien contigo, y nunca de noche.', tone: 'success' };
  }
}

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export type NoticeVisual = { icon: IconName; tone: 'success' | 'accent' | 'info' | 'primary' };

/** Icon and colour of each kind of notice in the inbox: offers are green, interviews orange, nudges blue. */
export function noticeVisual(kind: NotificationKind): NoticeVisual {
  switch (kind) {
    case 'newJobs':
      return { icon: 'sparkles', tone: 'success' };
    case 'interviewDayBefore':
    case 'interviewSoon':
      return { icon: 'calendar', tone: 'accent' };
    case 'followUp':
      return { icon: 'chatbubble-ellipses', tone: 'info' };
    default:
      return { icon: 'notifications', tone: 'primary' };
  }
}

export type NotificationTarget = { kind: 'job'; jobId: string } | { kind: 'inbox' };

/**
 * Where a tapped notice goes. The payload comes from the network, so it is validated: only a well-formed job id opens
 * a job, everything else falls back to the inbox.
 */
export function notificationTarget(data: unknown): NotificationTarget {
  if (data && typeof data === 'object') {
    const jobId = (data as Record<string, unknown>).jobId;
    if (typeof jobId === 'string' && UUID.test(jobId)) return { kind: 'job', jobId };
  }
  return { kind: 'inbox' };
}

/** Badge text for the bell: nothing at zero, capped so it never overflows. */
export function unreadBadge(count: number): string | null {
  if (!Number.isFinite(count) || count <= 0) return null;
  return count > 9 ? '9+' : String(Math.floor(count));
}

/** What the "send me a test" button reports back, in plain words. */
export function testResultMessage(result: { devices: number; accepted: number; pushConfigured: boolean }): string {
  if (result.accepted > 0) return 'Enviamos un aviso de prueba a tu celular. Debería llegar en unos segundos.';
  if (result.devices === 0) return 'Quedó en tu bandeja. Este celular aún no está registrado para recibir avisos.';
  if (!result.pushConfigured) return 'Quedó en tu bandeja. El envío al celular aún no está activo en el servidor.';
  return 'Quedó en tu bandeja, pero no pudimos entregarlo al celular. Prueba de nuevo en un rato.';
}
