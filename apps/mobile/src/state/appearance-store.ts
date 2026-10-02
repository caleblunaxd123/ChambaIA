import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';
import { create } from 'zustand';

export type AppearancePreference = 'system' | 'light' | 'dark';

const KEY = 'chambaia.appearance.v1';

const isPreference = (value: unknown): value is AppearancePreference => value === 'system' || value === 'light' || value === 'dark';

type AppearanceState = {
  preference: AppearancePreference;
  /** Reads the saved choice once at app start. Never throws: the default ("follow the phone") always works. */
  hydrate: () => Promise<void>;
  setPreference: (preference: AppearancePreference) => void;
};

export const useAppearance = create<AppearanceState>((set) => ({
  preference: 'system',

  hydrate: async () => {
    try {
      const raw = Platform.OS === 'web' ? globalThis.localStorage?.getItem(KEY) : await SecureStore.getItemAsync(KEY);
      if (isPreference(raw)) set({ preference: raw });
    } catch {
      // Keep the default.
    }
  },

  setPreference: (preference) => {
    set({ preference });
    try {
      if (Platform.OS === 'web') globalThis.localStorage?.setItem(KEY, preference);
      else void SecureStore.setItemAsync(KEY, preference).catch(() => undefined);
    } catch {
      // The in-memory choice still applies for this session.
    }
  },
}));
