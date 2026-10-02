import { useRouter } from 'expo-router';
import { View } from 'react-native';

import { useMarkAllNotificationsRead, useMarkNotificationRead, useNotificationSummary, useNotifications, usePreferences, useSendTestNotification } from '@/api/queries';
import type { AppNotification } from '@/api/schemas';
import { jobsLink } from '@/features/jobs/links';
import { openSystemSettings, syncPush } from '@/features/notifications/push-service';
import { describePushStatus, testResultMessage } from '@/features/notifications/push';
import { formatRelativeTime } from '@/lib/format';
import { haptics } from '@/lib/haptics';
import { usePushStore } from '@/state/push-store';
import { toast } from '@/state/toast-store';
import { Button, IconButton } from '@/ui/Button';
import { Card } from '@/ui/Card';
import { EmptyState, ErrorState } from '@/ui/EmptyState';
import { Icon } from '@/ui/Icon';
import { Screen } from '@/ui/Screen';
import { JobListSkeleton } from '@/ui/Skeleton';
import { Text } from '@/ui/Text';
import { useTheme } from '@/ui/theme';

export default function NotificationsScreen() {
  const router = useRouter();
  const list = useNotifications();
  const summary = useNotificationSummary();
  const markRead = useMarkNotificationRead();
  const markAll = useMarkAllNotificationsRead();

  const items = list.data?.pages.flatMap((p) => p.items) ?? [];
  const unread = summary.data?.unread ?? items.filter((n) => !n.readAt).length;
  const back = () => (router.canGoBack() ? router.back() : router.replace('/'));

  const open = (n: AppNotification) => {
    if (!n.readAt) markRead.mutate(n.id);
    if (n.jobId) router.push({ pathname: '/job/[id]', params: { id: n.jobId } });
    else if (n.kind === 'newJobs') router.navigate(jobsLink('new'));
  };

  return (
    <Screen onRefresh={() => { void list.refetch(); void summary.refetch(); }} refreshing={list.isRefetching}>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 12 }}>
        <IconButton icon="chevron-back" label="Volver" onPress={back} testID="notifications-back" />
        <Text variant="title" style={{ flex: 1 }}>Avisos</Text>
        {unread > 0 ? (
          <Button label="Marcar leídos" variant="ghost" size="sm" onPress={() => markAll.mutate()} loading={markAll.isPending} testID="notifications-read-all" />
        ) : null}
      </View>

      <PushStatusCard />

      {list.isLoading ? (
        <JobListSkeleton count={3} />
      ) : list.isError ? (
        <ErrorState message={list.error.message} onRetry={() => void list.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState
          testID="notifications-empty"
          icon="notifications-outline"
          title="Aún no hay avisos"
          message="Cuando aparezca una oferta que encaje muy bien contigo, tu agente te lo dirá aquí y en tu celular."
        />
      ) : (
        <View style={{ gap: 10 }}>
          {items.map((n) => <NotificationRow key={n.id} notification={n} onPress={() => open(n)} />)}
          {list.hasNextPage ? <Button label="Ver más" variant="secondary" onPress={() => void list.fetchNextPage()} loading={list.isFetchingNextPage} /> : null}
        </View>
      )}
    </Screen>
  );
}

function NotificationRow({ notification: n, onPress }: { notification: AppNotification; onPress: () => void }) {
  const { colors } = useTheme();
  const unread = n.readAt === null;
  return (
    <Card onPress={onPress} testID="notification-row" accessibilityLabel={`${unread ? 'Sin leer. ' : ''}${n.title}. ${n.body}`} style={{ flexDirection: 'row', gap: 12, alignItems: 'flex-start' }}>
      <View style={{ width: 40, height: 40, borderRadius: 14, backgroundColor: n.kind === 'newJobs' ? colors.successTint : colors.primaryTint, alignItems: 'center', justifyContent: 'center' }}>
        <Icon name={n.kind === 'newJobs' ? 'sparkles' : 'notifications'} size={20} tone={n.kind === 'newJobs' ? 'success' : 'primary'} />
      </View>
      <View style={{ flex: 1, gap: 2 }}>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
          <Text variant="bodyStrong" style={{ flex: 1 }} numberOfLines={1}>{n.title}</Text>
          {unread ? <View testID="notification-unread-dot" style={{ width: 9, height: 9, borderRadius: 5, backgroundColor: colors.accent }} /> : null}
        </View>
        <Text tone="muted" numberOfLines={3}>{n.body}</Text>
        <Text variant="caption" tone="subtle">{formatRelativeTime(n.createdAt)}</Text>
      </View>
    </Card>
  );
}

/** Honest status of alerts on this phone, with the one action that makes sense for the state, and a way to test them. */
function PushStatusCard() {
  const { colors } = useTheme();
  const status = usePushStore((s) => s.status);
  const prefs = usePreferences();
  const test = useSendTestNotification();
  const router = useRouter();

  const pushEnabled = prefs.data?.pushEnabled ?? true;
  const copy = describePushStatus(status, pushEnabled);
  const tint = { success: colors.successTint, warning: colors.warningTint, muted: colors.surfaceMuted }[copy.tone];
  const icon = copy.tone === 'success' ? 'notifications' : copy.tone === 'warning' ? 'notifications-off-outline' : 'notifications-outline';

  const sendTest = () =>
    test.mutate(undefined, {
      onSuccess: (result) => {
        haptics.success();
        toast.show({ message: testResultMessage(result), tone: result.accepted > 0 ? 'success' : 'default' });
      },
      onError: (error) => {
        haptics.error();
        toast.show({ message: error.message || 'No pudimos enviar la prueba.', tone: 'danger' });
      },
    });

  return (
    <Card tone="muted" testID="push-status" style={{ gap: 12 }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 12 }}>
        <View style={{ width: 40, height: 40, borderRadius: 14, backgroundColor: tint, alignItems: 'center', justifyContent: 'center' }}>
          <Icon name={icon} size={20} tone={copy.tone === 'muted' ? 'muted' : copy.tone} />
        </View>
        <View style={{ flex: 1, gap: 2 }}>
          <Text variant="bodyStrong" testID="push-status-title">{copy.title}</Text>
          <Text variant="caption" tone="muted">{copy.detail}</Text>
        </View>
      </View>

      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
        {pushEnabled && status.state === 'off' ? <Button label="Activar avisos" icon="notifications" size="sm" onPress={() => void syncPush({ ask: true })} testID="push-enable" /> : null}
        {pushEnabled && status.state === 'blocked' ? <Button label="Abrir ajustes" icon="settings-outline" size="sm" onPress={openSystemSettings} /> : null}
        {pushEnabled && status.state === 'error' ? <Button label="Reintentar" size="sm" onPress={() => void syncPush({ ask: true })} /> : null}
        {!pushEnabled ? <Button label="Activar en mis preferencias" size="sm" variant="secondary" onPress={() => router.push('/edit-preferences')} /> : null}
        <Button label="Enviar aviso de prueba" variant="secondary" size="sm" onPress={sendTest} loading={test.isPending} testID="push-test" />
      </View>
    </Card>
  );
}
