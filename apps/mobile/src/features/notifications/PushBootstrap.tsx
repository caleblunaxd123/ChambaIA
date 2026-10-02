import { useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { useEffect, useRef } from 'react';

import { keys, usePreferences } from '@/api/queries';
import { useAuthStore } from '@/state/auth-store';

import { loadNotifications, syncPush } from './push-service';
import { notificationTarget } from './push';

type Tapped = { notification: { request: { identifier: string; content: { data?: unknown } } } };

/**
 * Mounted once under the signed-in app. It (1) registers this phone when alerts are on and the permission is already
 * granted, (2) refreshes the bell when a notice arrives, and (3) sends a tapped notice to the right screen, including
 * when the tap is what launched the app. Renders nothing.
 */
export function PushBootstrap() {
  const router = useRouter();
  const client = useQueryClient();
  const signedIn = useAuthStore((s) => s.status === 'signedIn');
  const prefs = usePreferences();
  const pushEnabled = prefs.data?.pushEnabled;
  const handled = useRef(new Set<string>());

  useEffect(() => {
    if (signedIn && pushEnabled) void syncPush({ ask: false });
  }, [signedIn, pushEnabled]);

  useEffect(() => {
    if (!signedIn) return;
    let cancelled = false;
    let cleanup: (() => void) | undefined;

    const open = (response: Tapped) => {
      const id = response.notification.request.identifier;
      if (handled.current.has(id)) return;
      handled.current.add(id);
      void client.invalidateQueries({ queryKey: keys.notifications });
      const target = notificationTarget(response.notification.request.content.data);
      if (target.kind === 'job') router.push({ pathname: '/job/[id]', params: { id: target.jobId } });
      else router.push('/notifications');
    };

    void loadNotifications().then((Notifications) => {
      if (!Notifications || cancelled) return;
      const received = Notifications.addNotificationReceivedListener(() => void client.invalidateQueries({ queryKey: keys.notifications }));
      const tapped = Notifications.addNotificationResponseReceivedListener((response) => open(response));
      const launchedBy = Notifications.getLastNotificationResponse();
      if (launchedBy) open(launchedBy);
      cleanup = () => {
        received.remove();
        tapped.remove();
      };
    });

    return () => {
      cancelled = true;
      cleanup?.();
    };
  }, [signedIn, client, router]);

  return null;
}
