import { create } from 'zustand';

import type { AgentCommand } from '@/api/schemas';

export type ChatMessage = {
  id: string;
  from: 'user' | 'agent';
  text: string;
  /** Agent replies that changed preferences carry the list of changes (shown as chips). */
  changes?: string[];
  failed?: boolean;
  /** What the AI understood. Nothing changes until the person approves it. */
  proposal?: { commands: AgentCommand[]; descriptions: string[]; state: 'pending' | 'applied' | 'dismissed' };
};

type ChatState = {
  messages: ChatMessage[];
  push: (message: Omit<ChatMessage, 'id'>) => void;
  resolveProposal: (id: string, state: 'applied' | 'dismissed') => void;
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
  resolveProposal: (id, state) =>
    set((s) => ({ messages: s.messages.map((m) => (m.id === id && m.proposal ? { ...m, proposal: { ...m.proposal, state } } : m)) })),
  reset: () => set({ messages: [WELCOME] }),
}));
