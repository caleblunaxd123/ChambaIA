import { applicationEventSchema, notificationSchema } from '@/api/schemas';
import type { ApplicationEvent, ApplicationStatus } from '@/api/schemas';
import { describeEvent } from '@/features/applications/history';
import { noticeVisual } from '@/features/notifications/push';

const NOW = new Date(2026, 9, 2, 12, 0, 0);

function status(from: ApplicationStatus | null, to: ApplicationStatus): ApplicationEvent {
  return { id: 'e', kind: 'statusChanged', fromStatus: from, toStatus: to, interviewDate: null, at: new Date(2026, 9, 2, 9, 30).toISOString() };
}

describe('application history wording', () => {
  it('says what happened when the card moves forward or is created', () => {
    expect(describeEvent(status(null, 'interested'), NOW).text).toBe('La guardaste como interesante');
    expect(describeEvent(status('interested', 'applied'), NOW).text).toBe('Marcaste que postulaste');
    expect(describeEvent(status('applied', 'interview'), NOW).text).toBe('Pasó a la etapa de entrevista');
    expect(describeEvent(status('interview', 'offer'), NOW).text).toBe('Recibiste una oferta');
    expect(describeEvent(status('applied', 'discarded'), NOW).text).toBe('La descartaste');
  });

  it('spells out both ends when the card goes back or comes back to life', () => {
    expect(describeEvent(status('applied', 'interested'), NOW).text).toBe('Pasó de «Postulado» a «Interesado»');
    expect(describeEvent(status('discarded', 'interested'), NOW).text).toBe('Pasó de «Descartado» a «Interesado»');
    expect(describeEvent(status('discarded', 'found'), NOW).text).toBe('Pasó de «Descartado» a «Encontrada»');
  });

  it('tells interview dates and their removal', () => {
    const scheduled: ApplicationEvent = {
      id: 'e', kind: 'interviewScheduled', fromStatus: null, toStatus: null,
      interviewDate: new Date(2026, 9, 5, 15, 0).toISOString(), at: new Date(2026, 9, 2, 9, 0).toISOString(),
    };
    expect(describeEvent(scheduled, NOW).text).toMatch(/^Agendaste la entrevista para .*15:00$/);
    expect(describeEvent({ ...scheduled, kind: 'interviewCleared', interviewDate: null }, NOW).text).toBe('Quitaste la fecha de la entrevista');
  });

  it('shows when it happened in local words', () => {
    expect(describeEvent(status(null, 'interested'), NOW).when).toBe('Hoy, 09:30');
  });
});

describe('notice visuals', () => {
  it('gives every kind an icon and a tone', () => {
    expect(noticeVisual('newJobs')).toEqual({ icon: 'sparkles', tone: 'success' });
    expect(noticeVisual('interviewDayBefore').tone).toBe('accent');
    expect(noticeVisual('interviewSoon').icon).toBe('calendar');
    expect(noticeVisual('followUp').tone).toBe('info');
    expect(noticeVisual('test').icon).toBe('notifications');
  });
});

describe('contract', () => {
  it('parses the new notice kinds and history events from the API', () => {
    const base = { id: 'a', title: 't', body: 'b', jobId: 'j', matchCount: 0, strongCount: 0, createdAt: '2026-10-01T10:00:00Z', readAt: null };
    for (const kind of ['interviewDayBefore', 'interviewSoon', 'followUp']) expect(notificationSchema.parse({ ...base, kind }).kind).toBe(kind);

    const event = applicationEventSchema.parse({ id: 'e', kind: 'interviewScheduled', fromStatus: null, toStatus: null, interviewDate: '2026-10-05T20:00:00Z', at: '2026-10-02T10:00:00Z' });
    expect(event.kind).toBe('interviewScheduled');
    expect(() => applicationEventSchema.parse({ ...event, kind: 'deleted' })).toThrow();
  });
});
