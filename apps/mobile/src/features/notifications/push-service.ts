import Constants, { ExecutionEnvironment } from 'expo-constants';
import * as Device from 'expo-device';
import { Linking, Platform } from 'react-native';

import { api } from '@/api/endpoints';
import { usePushStore } from '@/state/push-store';

import { type PushAvailability, permissionState, pushAvailability } from './push';

/**
 * The native side of push. `expo-notifications` is imported lazily and only when this device can use it: in Expo Go on
 * Android, on the web or on an emulator the module is never touched, so those environments stay quiet instead of logging
 * errors. Everything else (rules, wording, routing) is pure and lives in push.ts.
 */

const CHANNEL_ID = 'default';

type NotificationsModule = typeof import('expo-notifications');

let handlerConfigured = false;

export function currentAvailability(): PushAvailability {
  const projectId = (Constants.expoConfig?.extra as { eas?: { projectId?: string } } | undefined)?.eas?.projectId ?? Constants.easConfig?.projectId;
  return pushAvailability({
    os: Platform.OS,
    isDevice: Device.isDevice,
    isExpoGo: Constants.executionEnvironment === ExecutionEnvironment.StoreClient,
    projectId,
  });
}

/** Returns the notifications module only when this device supports it. */
export async function loadNotifications(): Promise<NotificationsModule | null> {
  if (!currentAvailability().ok) return null;
  const Notifications = await import('expo-notifications');
  if (!handlerConfigured) {
    handlerConfigured = true;
    // A notice that arrives while the app is open is shown, quietly: no sound, no badge counting.
    Notifications.setNotificationHandler({
      handleNotification: async () => ({ shouldShowBanner: true, shouldShowList: true, shouldPlaySound: false, shouldSetBadge: false }),
    });
  }
  return Notifications;
}

/**
 * Brings this phone to "registered for alerts" as far as the user allows. With `ask: false` it never shows the system
 * permission dialog (used at start-up: asking out of the blue is how apps lose the permission); with `ask: true` it does
 * (used when the user taps "Activar avisos").
 */
export async function syncPush({ ask }: { ask: boolean }): Promise<void> {
  const { set } = usePushStore.getState();
  const availability = currentAvailability();
  if (!availability.ok) {
    set({ status: { state: 'unavailable', reason: availability.reason, message: availability.message } });
    return;
  }

  try {
    const Notifications = (await loadNotifications())!;

    if (Platform.OS === 'android') {
      await Notifications.setNotificationChannelAsync(CHANNEL_ID, {
        name: 'Oportunidades para ti',
        importance: Notifications.AndroidImportance.DEFAULT,
      });
    }

    let state = permissionState(await Notifications.getPermissionsAsync());
    if (state === 'ask' && ask) state = permissionState(await Notifications.requestPermissionsAsync());
    if (state !== 'granted') {
      set({ status: { state: state === 'blocked' ? 'blocked' : 'off' } });
      return;
    }

    const { data: token } = await Notifications.getExpoPushTokenAsync({ projectId: availability.projectId });
    await api.devices.register(token, Platform.OS === 'ios' ? 'ios' : 'android');
    set({ token, status: { state: 'active' } });
  } catch {
    set({ status: { state: 'error' } });
  }
}

/** On sign-out: this phone stops receiving the signed-out account's alerts. Best effort: the server also moves tokens between accounts. */
export async function unregisterPush(): Promise<void> {
  const { token, set } = usePushStore.getState();
  set({ token: null, status: { state: 'checking' } });
  if (!token) return;
  try {
    await api.devices.unregister(token);
  } catch {
    // Offline sign-out must still work.
  }
}

export function openSystemSettings() {
  void Linking.openSettings();
}
