import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

import type { AuthResponse } from '@/api/schemas';

const KEY = 'chambaia.session.v1';

/**
 * Tokens live in the OS keystore (Keychain / Keystore) on devices.
 * Web has no secure storage: localStorage is only used there for local development.
 */
export const sessionStorage = {
  async load(): Promise<AuthResponse | null> {
    try {
      const raw = Platform.OS === 'web' ? globalThis.localStorage?.getItem(KEY) : await SecureStore.getItemAsync(KEY);
      return raw ? (JSON.parse(raw) as AuthResponse) : null;
    } catch {
      return null;
    }
  },

  async save(session: AuthResponse): Promise<void> {
    const raw = JSON.stringify(session);
    try {
      if (Platform.OS === 'web') globalThis.localStorage?.setItem(KEY, raw);
      else await SecureStore.setItemAsync(KEY, raw);
    } catch {
      // Storage is a convenience: the in-memory session keeps working.
    }
  },

  async clear(): Promise<void> {
    try {
      if (Platform.OS === 'web') globalThis.localStorage?.removeItem(KEY);
      else await SecureStore.deleteItemAsync(KEY);
    } catch {
      // Nothing else to do.
    }
  },
};
