import {
  canRespondToExit,
  jobExitReasonLabel,
  jobExitReasonText,
  jobExitReasons,
  jobExitTitle,
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

  it('counts abandonments and interruptions with Polish plurals', () => {
    expect(reliabilityLabel(null)).toBe('');
    expect(reliabilityLabel({ abandonedCount: 0, interruptedCount: 0 })).toBe('');
    expect(reliabilityLabel({ abandonedCount: 1, interruptedCount: 0 })).toBe('1 rezygnacja');
    expect(reliabilityLabel({ abandonedCount: 3, interruptedCount: 5 })).toBe('3 rezygnacje · 5 przerwanych zleceń');
    expect(reliabilityLabel({ abandonedCount: 0, interruptedCount: 2 })).toBe('2 przerwane zlecenia');
  });
});
