import type { FooterContext } from './announcement-footer';
import { footerMode } from './announcement-footer';

describe('footerMode', () => {
  const guest: FooterContext = {
    status: 'Open',
    isOwner: false,
    isParticipant: false,
    interrupted: false,
    abandoned: false,
  };
  const owner: FooterContext = { ...guest, isOwner: true, isParticipant: true };
  const acceptedHelper: FooterContext = { ...guest, isParticipant: true };

  it('lets a guest or a helper ask and make an offer while the announcement is open', () => {
    expect(footerMode(guest)).toBe('open');
    expect(footerMode({ ...guest, status: 'Accepted' })).toBe('closed');
    expect(footerMode({ ...guest, status: 'Cancelled' })).toBe('closed');
  });

  it('gives the owner his own footer outside of a job', () => {
    expect(footerMode(owner)).toBe('owner');
    expect(footerMode({ ...owner, status: 'Cancelled' })).toBe('owner');
  });

  it('shows the job step to both participants of an accepted job', () => {
    expect(footerMode({ ...owner, status: 'Accepted' })).toBe('job');
    expect(footerMode({ ...acceptedHelper, status: 'InProgress' })).toBe('job');
  });

  it('asks the participants to rate after the job is completed', () => {
    expect(footerMode({ ...owner, status: 'Completed' })).toBe('completed');
    expect(footerMode({ ...acceptedHelper, status: 'Completed' })).toBe('completed');
    // Obcy pomocnik widzi tylko, że ogłoszenie jest zamknięte.
    expect(footerMode({ ...guest, status: 'Completed' })).toBe('closed');
  });

  it('puts the interruption before everything else for the participants', () => {
    expect(footerMode({ ...owner, status: 'Interrupted', interrupted: true })).toBe('interrupted');
    expect(footerMode({ ...acceptedHelper, status: 'Interrupted', interrupted: true })).toBe('interrupted');
    expect(footerMode({ ...guest, status: 'Interrupted', interrupted: true })).toBe('closed');
  });

  it('tells a helper whose job was abandoned that he cannot offer again', () => {
    expect(footerMode({ ...guest, abandoned: true })).toBe('abandoned');
  });
});
