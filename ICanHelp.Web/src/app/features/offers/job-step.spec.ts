import { jobStep } from './job-step';

describe('jobStep', () => {
  it('lets only the helper start an accepted job; the client waits but can still confirm', () => {
    expect(jobStep('Accepted', 'Helper', undefined)).toEqual({
      action: 'start',
      primary: true,
      note: null,
      highlight: false,
      exit: 'abandon',
    });
    const client = jobStep('Accepted', 'Client', undefined);
    expect(client.action).toBe('complete');
    expect(client.primary).toBe(false);
    expect(client.note).toContain('pomocnik rozpocznie');
  });

  it('lets the helper report the work as done once and then wait', () => {
    expect(jobStep('InProgress', 'Helper', undefined).action).toBe('report');
    const waiting = jobStep('InProgress', 'Helper', '2026-11-13T10:00:00Z');
    expect(waiting.action).toBeNull();
    expect(waiting.note).toContain('czekasz na potwierdzenie');
  });

  it('highlights the confirmation for the client after the helper reported the work', () => {
    const before = jobStep('InProgress', 'Client', undefined);
    expect(before).toMatchObject({ action: 'complete', primary: false, highlight: false });
    const after = jobStep('InProgress', 'Client', '2026-11-13T10:00:00Z');
    expect(after).toMatchObject({ action: 'complete', primary: true, highlight: true });
    expect(after.note).toContain('Pomocnik zgłosił wykonanie');
  });

  it('gives both sides a way out: abandon before the start, interrupt during the work', () => {
    expect(jobStep('Accepted', 'Client', undefined).exit).toBe('abandon');
    expect(jobStep('Accepted', 'Helper', undefined).exit).toBe('abandon');
    expect(jobStep('InProgress', 'Client', undefined).exit).toBe('interrupt');
    expect(jobStep('InProgress', 'Helper', '2026-11-13T10:00:00Z').exit).toBe('interrupt');
  });

  it('has nothing to do outside of the job', () => {
    for (const status of ['Open', 'Completed', 'Cancelled', 'Interrupted']) {
      expect(jobStep(status, 'Client', undefined).action).toBeNull();
      expect(jobStep(status, 'Helper', undefined).note).toBeNull();
      expect(jobStep(status, 'Helper', undefined).exit).toBeNull();
    }
  });
});
