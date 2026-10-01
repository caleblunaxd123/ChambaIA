import { create } from 'zustand';

export type ChatMessage = {
  id: string;
  from: 'user' | 'agent';
  text: string;
  /** Agent replies that changed preferences carry the list of changes (shown as chips). */
  changes?: string[];
  failed?: boolean;
};

type ChatState = {
  messages: ChatMessage[];
  push: (message: Omit<ChatMessage, 'id'>) => void;
  reset: () => void;
};

const WELCOME: ChatMessage = {
  id: 'welcome',
  from: 'agent',
  text: 'Hola, soy tu agente. Cuéntame qué buscas o qué no quieres ver y lo recordaré. Por ejemplo: «no me muestres trabajos en Ate» o «mínimo 1800 soles».',
};

let counter = 0;

/** Conversation lives in memory: the durable result of each message is the updated preferences, not the transcript. */
export const useAgentChat = create<ChatState>((set) => ({
  messages: [WELCOME],
  push: (message) => set((s) => ({ messages: [...s.messages, { ...message, id: `m${++counter}` }] })),
  reset: () => set({ messages: [WELCOME] }),
}));
