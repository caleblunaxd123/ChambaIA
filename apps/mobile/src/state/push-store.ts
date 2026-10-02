import { create } from 'zustand';

import type { PushStatus } from '@/features/notifications/push';

type PushState = {
  status: PushStatus;
  /** The Expo push token registered by this installation (kept only in memory: it is re-derived on every start). */
  token: string | null;
  set: (patch: Partial<Pick<PushState, 'status' | 'token'>>) => void;
};

export const usePushStore = create<PushState>((set) => ({
  status: { state: 'checking' },
  token: null,
  set: (patch) => set(patch),
}));
