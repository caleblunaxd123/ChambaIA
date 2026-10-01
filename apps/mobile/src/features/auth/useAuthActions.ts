import { useMutation } from '@tanstack/react-query';

import { api } from '@/api/endpoints';
import { useAgentChat } from '@/state/agent-chat';
import { useAuthStore } from '@/state/auth-store';

export function useLogin() {
  const setSession = useAuthStore((s) => s.setSession);
  return useMutation({
    mutationFn: api.auth.login,
    onSuccess: (session) => setSession(session),
  });
}

export function useRegister() {
  const setSession = useAuthStore((s) => s.setSession);
  return useMutation({
    mutationFn: api.auth.register,
    onSuccess: (session) => setSession(session),
  });
}

/** Revokes the refresh token on the server (best effort) and always clears the local session. */
export async function logout() {
  const { refreshToken, signOut } = useAuthStore.getState();
  try {
    if (refreshToken) await api.auth.logout(refreshToken);
  } catch {
    // Offline logout must still work locally.
  }
  useAgentChat.getState().reset();
  await signOut();
}

export function useDeleteAccount() {
  return useMutation({
    mutationFn: (password: string) => api.account.remove(password),
    onSuccess: async () => {
      useAgentChat.getState().reset();
      await useAuthStore.getState().signOut();
    },
  });
}
