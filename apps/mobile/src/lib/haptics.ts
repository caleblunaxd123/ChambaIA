import * as Haptics from 'expo-haptics';
import { Platform } from 'react-native';

const enabled = Platform.OS === 'ios' || Platform.OS === 'android';

/** Haptics are a nicety: never throw, never run on web. */
export const haptics = {
  tap: () => {
    if (enabled) void Haptics.selectionAsync().catch(() => undefined);
  },
  success: () => {
    if (enabled) void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success).catch(() => undefined);
  },
  warning: () => {
    if (enabled) void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Warning).catch(() => undefined);
  },
  error: () => {
    if (enabled) void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Error).catch(() => undefined);
  },
};
