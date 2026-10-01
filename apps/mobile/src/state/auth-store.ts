import { create } from 'zustand';

import type { AuthResponse, User } from '@/api/schemas';

import { sessionStorage } from './session-storage';

export type AuthStatus = 'loading' | 'signedIn' | 'signedOut';

type AuthState = {
  status: AuthStatus;
  user: User | null;
  accessToken: string | null;
  refreshToken: string | null;
  /** Reads the persisted session once at app start. */
  hydrate: () => Promise<void>;
  setSession: (session: AuthResponse) => Promise<void>;
  signOut: () => Promise<void>;
};

export const useAuthStore = create<AuthState>((set) => ({
  status: 'loading',
  user: null,
  accessToken: null,
  refreshToken: null,

  hydrate: async () => {
    const session = await sessionStorage.load();
    if (session) set({ status: 'signedIn', user: session.user, accessToken: session.accessToken, refreshToken: session.refreshToken });
    else set({ status: 'signedOut' });
  },

  setSession: async (session) => {
    set({ status: 'signedIn', user: session.user, accessToken: session.accessToken, refreshToken: session.refreshToken });
    await sessionStorage.save(session);
  },

  signOut: async () => {
    set({ status: 'signedOut', user: null, accessToken: null, refreshToken: null });
    await sessionStorage.clear();
  },
}));
