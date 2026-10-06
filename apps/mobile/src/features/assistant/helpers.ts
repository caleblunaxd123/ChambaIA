import type { PrepPreview } from '@/api/schemas';

/** How many AI drafts are left today, in plain words. Never hides that the allowance is limited. */
export function quotaLabel(quota: PrepPreview['quota']): string {
  const left = quota.remainingToday;
  if (quota.dailyLimit <= 0) return 'Los borradores con IA no están incluidos en tu plan.';
  if (left <= 0) return `Hoy ya usaste tus ${quota.dailyLimit} borradores. Vuelve mañana.`;
  return `Te ${left === 1 ? 'queda 1 borrador' : `quedan ${left} borradores`} de ${quota.dailyLimit} hoy.`;
}

export function canGenerate(preview: PrepPreview | undefined): boolean {
  return preview !== undefined && preview.available && preview.quota.remainingToday > 0;
}

export type Tone = 'formal' | 'cercano';

export const toneLabel: Record<Tone, string> = { formal: 'Formal', cercano: 'Cercano' };

/** What "copy" puts on the clipboard: the message alone, ready to paste. The analysis stays in the app. */
export function copyableMessage(message: string): string {
  return message.trim();
}

/** The proposal card needs one short line per change; an empty list means there is nothing to approve. */
export function hasProposal(proposal: { descriptions: string[] } | null | undefined): proposal is { descriptions: string[] } {
  return proposal != null && proposal.descriptions.length > 0;
}
