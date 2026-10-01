import { useRouter } from 'expo-router';
import { useCallback } from 'react';

import { useDismissMutation, useInterestedMutation } from '@/api/queries';
import { haptics } from '@/lib/haptics';

/** Shared card actions (open / me interesa / descartar) so Home, feed and detail behave identically. */
export function useJobActions() {
  const router = useRouter();
  const interested = useInterestedMutation();
  const dismiss = useDismissMutation();

  const open = useCallback((id: string) => router.push({ pathname: '/job/[id]', params: { id } }), [router]);

  const save = useCallback((id: string) => interested.mutate(id, { onSuccess: () => haptics.success(), onError: () => haptics.error() }), [interested]);

  const discard = useCallback((id: string) => dismiss.mutate(id, { onSuccess: () => haptics.warning(), onError: () => haptics.error() }), [dismiss]);

  return { open, save, discard, busy: interested.isPending || dismiss.isPending };
}
