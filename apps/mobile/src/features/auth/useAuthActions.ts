import { useMutation } from '@tanstack/react-query';

import { api } from '@/api/endpoints';
import { unregisterPush } from '@/features/notifications/push-service';
import { useAgentChat } from '@/state/agent-chat';
import { useAuthStore } from '@/state/auth-store';
import { usePushStore } from '@/state/push-store';

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
  await unregisterPush(); // while the session is still valid: this phone stops getting this account's alerts
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
      usePushStore.getState().set({ token: null, status: { state: 'checking' } }); // the server already dropped the tokens with the account
      useAgentChat.getState().reset();
      await useAuthStore.getState().signOut();
    },
  });
}
