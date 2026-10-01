import { create } from 'zustand';

export type ToastTone = 'default' | 'success' | 'danger';

export type Toast = {
  id: number;
  message: string;
  tone: ToastTone;
  /** One optional action, typically "Deshacer" or "Ver". */
  action?: { label: string; onPress: () => void };
  durationMs: number;
};

type ToastState = {
  current: Toast | null;
  show: (toast: Omit<Toast, 'id' | 'tone' | 'durationMs'> & { tone?: ToastTone; durationMs?: number }) => void;
  dismiss: (id?: number) => void;
};

let counter = 0;

/** A single toast at a time: a new one replaces the previous (the latest action is the one the user cares about). */
export const useToast = create<ToastState>((set, get) => ({
  current: null,
  show: ({ tone = 'default', durationMs, ...rest }) =>
    set({ current: { id: ++counter, tone, durationMs: durationMs ?? (rest.action ? 5000 : 3000), ...rest } }),
  dismiss: (id) => {
    const current = get().current;
    if (current && (id === undefined || current.id === id)) set({ current: null });
  },
}));

/** Imperative helper for places that are not components (mutation callbacks). */
export const toast = {
  show: (...args: Parameters<ToastState['show']>) => useToast.getState().show(...args),
  dismiss: () => useToast.getState().dismiss(),
};
