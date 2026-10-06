import { agentReplySchema, prepDraftSchema, prepPreviewSchema } from '@/api/schemas';
import type { PrepPreview } from '@/api/schemas';
import { canGenerate, copyableMessage, hasProposal, quotaLabel } from '@/features/assistant/helpers';
import { useAgentChat } from '@/state/agent-chat';

const quota = (callsToday: number, dailyLimit = 3) => ({ callsToday, dailyLimit, remainingToday: Math.max(0, dailyLimit - callsToday) });

const preview = (overrides: Partial<PrepPreview> = {}): PrepPreview => ({
  available: true, unavailableReason: null, payload: 'OFERTA…', includes: ['El cargo'], excludes: ['Tu correo'], quota: quota(0), ...overrides,
});

describe('quota wording', () => {
  it('says how many drafts are left, in the singular and the plural', () => {
    expect(quotaLabel(quota(0))).toBe('Te quedan 3 borradores de 3 hoy.');
    expect(quotaLabel(quota(2))).toBe('Te queda 1 borrador de 3 hoy.');
  });

  it('is honest when the allowance is gone or not part of the plan', () => {
    expect(quotaLabel(quota(3))).toBe('Hoy ya usaste tus 3 borradores. Vuelve mañana.');
    expect(quotaLabel(quota(0, 0))).toMatch(/no están incluidos/);
  });
});

describe('who may generate', () => {
  it('needs AI available and quota left', () => {
    expect(canGenerate(preview())).toBe(true);
    expect(canGenerate(preview({ available: false }))).toBe(false);
    expect(canGenerate(preview({ quota: quota(3) }))).toBe(false);
    expect(canGenerate(undefined)).toBe(false);
  });
});

describe('clipboard text and proposals', () => {
  it('copies the message alone, trimmed', () => {
    expect(copyableMessage('  Estimados señores, me interesa el puesto.\n')).toBe('Estimados señores, me interesa el puesto.');
  });

  it('only shows an approval card when there is something to approve', () => {
    expect(hasProposal(null)).toBe(false);
    expect(hasProposal(undefined)).toBe(false);
    expect(hasProposal({ descriptions: [] })).toBe(false);
    expect(hasProposal({ descriptions: ['Sueldo mínimo: S/ 2,000.'] })).toBe(true);
  });
});

describe('contract', () => {
  const overview = null;

  it('parses a reply with a proposal and one without', () => {
    const withProposal = agentReplySchema.parse({
      reply: 'Entendí esto…', understood: true, changes: [], overview,
      proposal: { commands: [{ intent: 'ExcludeDistrict', text: 'Ate', number: null, modality: null, flag: null }], descriptions: ['Ya no verás trabajos en Ate.'] },
    });
    expect(withProposal.proposal?.commands[0]?.intent).toBe('ExcludeDistrict');

    const plain = agentReplySchema.parse({ reply: 'Listo.', understood: true, changes: ['x'], overview });
    expect(plain.proposal ?? null).toBeNull();
    expect(agentReplySchema.parse({ reply: 'Listo.', understood: true, changes: [], overview, proposal: null }).proposal).toBeNull();
  });

  it('parses the preview and the draft the API sends', () => {
    expect(prepPreviewSchema.parse(preview()).quota.remainingToday).toBe(3);
    const draft = prepDraftSchema.parse({ message: 'Hola', highlights: ['a'], gaps: [], questions: ['¿Por qué?'], warnings: ['Revisa X'] });
    expect(draft.warnings).toEqual(['Revisa X']);
  });

  it('rejects an incomplete draft loudly instead of rendering garbage', () => {
    expect(() => prepDraftSchema.parse({ message: 'Hola' })).toThrow();
  });
});

describe('agent chat store', () => {
  beforeEach(() => useAgentChat.getState().reset());

  it('resolves a proposal in place without touching other messages', () => {
    const { push } = useAgentChat.getState();
    push({ from: 'agent', text: 'Entendí esto', proposal: { commands: [], descriptions: ['x'], state: 'pending' } });
    push({ from: 'agent', text: 'Otro mensaje' });
    const target = useAgentChat.getState().messages.find((m) => m.proposal);

    useAgentChat.getState().resolveProposal(target!.id, 'applied');

    const after = useAgentChat.getState().messages;
    expect(after.find((m) => m.id === target!.id)?.proposal?.state).toBe('applied');
    expect(after.filter((m) => m.proposal)).toHaveLength(1);
    expect(after.at(-1)?.text).toBe('Otro mensaje');
  });
});
