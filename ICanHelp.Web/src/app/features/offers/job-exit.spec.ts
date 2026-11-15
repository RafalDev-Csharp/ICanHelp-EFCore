import type { JobExitDto } from '../../core/api/api-client';

import {
  canRespondToExit,
  jobExitReasonLabel,
  jobExitReasonText,
  jobExitReasons,
  jobExitResponseAuthor,
  jobExitTitle,
  jobExitView,
  reliabilityLabel,
} from './job-exit';

describe('job-exit', () => {
  it('offers "conflict" only when interrupting the work', () => {
    expect(jobExitReasons('abandon').map((r) => r.value)).toEqual([
      'CannotMakeIt',
      'TermsDiffer',
      'NoContact',
      'Other',
    ]);
    expect(jobExitReasons('interrupt').map((r) => r.value)).toContain('Disagreement');
    expect(jobExitReasons('abandon')[0].label).toBe('Nie mogę w tym terminie');
  });

  it('describes the reason with the note', () => {
    expect(jobExitReasonLabel('NoContact')).toBe('Brak kontaktu');
    expect(jobExitReasonText({ reason: 'NoContact', note: undefined })).toBe('Brak kontaktu');
    expect(jobExitReasonText({ reason: 'NoContact', note: 'Nie odbiera.' })).toBe('Brak kontaktu – Nie odbiera.');
    expect(jobExitReasonText({ reason: 'Other', note: 'Sam naprawiłem.' })).toBe('Sam naprawiłem.');
  });

  it('titles the entry from the reader perspective', () => {
    const exit = { kind: 'Abandoned', actorId: 'h1', actorRole: 'Helper' };
    expect(jobExitTitle(exit, 'h1')).toBe('Zrezygnowałeś ze zlecenia');
    expect(jobExitTitle(exit, 'o1')).toBe('Pomocnik zrezygnował ze zlecenia');
    expect(jobExitTitle({ kind: 'Interrupted', actorId: 'o1', actorRole: 'Client' }, 'h1')).toBe(
      'Zleceniodawca przerwał zlecenie',
    );
  });

  it('lets only the other side respond, once', () => {
    const byHelper = { actorRole: 'Helper', helperId: 'h1', response: undefined };
    expect(canRespondToExit(byHelper, 'o1', 'o1')).toBe(true);
    expect(canRespondToExit(byHelper, 'h1', 'o1')).toBe(false);
    expect(canRespondToExit({ ...byHelper, response: 'Już jest.' }, 'o1', 'o1')).toBe(false);
    const byOwner = { actorRole: 'Client', helperId: 'h1', response: undefined };
    expect(canRespondToExit(byOwner, 'h1', 'o1')).toBe(true);
    expect(canRespondToExit(byOwner, 'x', 'o1')).toBe(false);
  });

  it('builds the whole entry for the reader – the same in the thread and in the announcement', () => {
    const exit: JobExitDto = {
      number: 1,
      kind: 'Interrupted',
      offerId: 'o1',
      helperId: 'h1',
      actorId: 'h1',
      actorRole: 'Helper',
      reason: 'NoContact',
      note: 'Nie odbiera.',
      occurredAt: '2026-11-13T10:00:00Z',
      response: undefined,
      respondedAt: undefined,
    };

    expect(jobExitView(exit, 'o1', 'o1')).toEqual({
      exit,
      title: 'Pomocnik przerwał zlecenie',
      reason: 'Brak kontaktu – Nie odbiera.',
      mine: false,
      canRespond: true,
      responseAuthor: 'Stanowisko zleceniodawcy',
    });
    expect(jobExitView(exit, 'h1', 'o1')).toMatchObject({
      title: 'Przerwałeś zlecenie',
      mine: true,
      canRespond: false,
    });
    // Widz bez roli (niezalogowany) niczego nie dopisze.
    expect(jobExitView(exit, undefined, 'o1')).toMatchObject({ mine: false, canRespond: false });
    expect(jobExitResponseAuthor({ actorRole: 'Client' })).toBe('Stanowisko pomocnika');
  });

  it('counts abandonments and interruptions with Polish plurals', () => {
    expect(reliabilityLabel(null)).toBe('');
    expect(reliabilityLabel({ abandonedCount: 0, interruptedCount: 0 })).toBe('');
    expect(reliabilityLabel({ abandonedCount: 1, interruptedCount: 0 })).toBe('1 rezygnacja');
    expect(reliabilityLabel({ abandonedCount: 3, interruptedCount: 5 })).toBe('3 rezygnacje · 5 przerwanych zleceń');
    expect(reliabilityLabel({ abandonedCount: 0, interruptedCount: 2 })).toBe('2 przerwane zlecenia');
  });
});
