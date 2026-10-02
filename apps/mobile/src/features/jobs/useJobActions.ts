import { useRouter } from 'expo-router';
import { useCallback } from 'react';

import { useDismissMutation, useInterestedMutation, useResetMatchMutation } from '@/api/queries';
import type { MatchStatus } from '@/api/schemas';
import { haptics } from '@/lib/haptics';
import { toast } from '@/state/toast-store';

/**
 * Shared card actions (open / guardar / descartar) so Home, feed and detail behave identically. Every reaction is
 * optimistic and comes with a toast that can undo it: nothing the user taps by accident is lost.
 */
export function useJobActions() {
  const router = useRouter();
  const interested = useInterestedMutation();
  const dismiss = useDismissMutation();
  const reset = useResetMatchMutation();

  const open = useCallback((id: string) => router.push({ pathname: '/job/[id]', params: { id } }), [router]);

  const failed = useCallback((error: Error) => {
    haptics.error();
    toast.show({ message: error.message || 'No pudimos guardar el cambio. Inténtalo de nuevo.', tone: 'danger' });
  }, []);

  const undo = useCallback(
    (id: string, previous: MatchStatus | undefined) => {
      // Dismissing a saved offer: undo puts it back in "Guardadas", not just in the feed.
      if (previous === 'interested') interested.mutate(id, { onError: failed });
      else reset.mutate(id, { onError: failed });
    },
    [interested, reset, failed],
  );

  const save = useCallback(
    (id: string) =>
      interested.mutate(id, {
        onSuccess: () => {
          haptics.success();
          toast.show({
            message: 'Guardada en tus postulaciones',
            tone: 'success',
            action: { label: 'Deshacer', onPress: () => reset.mutate(id, { onError: failed }) },
          });
        },
        onError: failed,
      }),
    [interested, reset, failed],
  );

  /** Saved offers toggle back: "Guardada" → tap → no longer saved (with undo). */
  const unsave = useCallback(
    (id: string) =>
      reset.mutate(id, {
        onSuccess: () => {
          haptics.tap();
          toast.show({ message: 'Ya no está en guardadas', action: { label: 'Deshacer', onPress: () => interested.mutate(id, { onError: failed }) } });
        },
        onError: failed,
      }),
    [interested, reset, failed],
  );

  const discard = useCallback(
    (id: string, previous?: MatchStatus, after?: () => void) =>
      dismiss.mutate(id, {
        onSuccess: () => {
          haptics.warning();
          toast.show({ message: 'Oferta descartada. No volverá a aparecer.', action: { label: 'Deshacer', onPress: () => undo(id, previous) } });
          after?.();
        },
        onError: failed,
      }),
    [dismiss, undo, failed],
  );

  return { open, save, unsave, discard, busy: interested.isPending || dismiss.isPending || reset.isPending };
}
