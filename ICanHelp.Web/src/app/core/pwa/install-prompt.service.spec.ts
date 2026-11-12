import { TestBed } from '@angular/core/testing';

import type { BeforeInstallPromptEvent } from './install-prompt.service';
import { InstallPromptService, isIos } from './install-prompt.service';

function installEvent(outcome: 'accepted' | 'dismissed'): BeforeInstallPromptEvent {
  const event = new Event('beforeinstallprompt', { cancelable: true }) as BeforeInstallPromptEvent;
  Object.assign(event, { prompt: vi.fn(() => Promise.resolve()), userChoice: Promise.resolve({ outcome }) });
  return event;
}

describe('InstallPromptService', () => {
  afterEach(() => vi.restoreAllMocks());

  it('keeps the Android install event for its own button and installs on click', async () => {
    const service = TestBed.inject(InstallPromptService);
    expect(service.canPrompt()).toBe(false);

    const event = installEvent('accepted');
    window.dispatchEvent(event);

    expect(event.defaultPrevented).toBe(true);
    expect(service.canPrompt()).toBe(true);

    await expect(service.install()).resolves.toBe(true);
    expect(event.prompt).toHaveBeenCalled();
    expect(service.isStandalone()).toBe(true);
    expect(service.canPrompt()).toBe(false);
  });

  it('uses the event only once – dismissed means the button goes away', async () => {
    const service = TestBed.inject(InstallPromptService);
    window.dispatchEvent(installEvent('dismissed'));

    await expect(service.install()).resolves.toBe(false);
    await expect(service.install()).resolves.toBe(false);
    expect(service.canPrompt()).toBe(false);
  });

  it('hides the prompt after the app is installed', () => {
    const service = TestBed.inject(InstallPromptService);
    window.dispatchEvent(installEvent('accepted'));

    window.dispatchEvent(new Event('appinstalled'));

    expect(service.canPrompt()).toBe(false);
    expect(service.isStandalone()).toBe(true);
  });

  it('recognises an iPhone and an iPad pretending to be a Mac', () => {
    const userAgent = vi.spyOn(navigator, 'userAgent', 'get');
    // jsdom nie ma `maxTouchPoints` – dokładamy na czas testu.
    let touchPoints = 5;
    Object.defineProperty(navigator, 'maxTouchPoints', { configurable: true, get: () => touchPoints });

    userAgent.mockReturnValue('Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15');
    expect(isIos()).toBe(true);
    userAgent.mockReturnValue('Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15');
    expect(isIos()).toBe(true);
    touchPoints = 0;
    expect(isIos()).toBe(false);
    userAgent.mockReturnValue('Mozilla/5.0 (Linux; Android 15; Pixel 7) Chrome/140.0');
    expect(isIos()).toBe(false);
    delete (navigator as { maxTouchPoints?: number }).maxTouchPoints;
  });

  it('shows the iPhone instruction only outside the home screen app', () => {
    vi.spyOn(navigator, 'userAgent', 'get').mockReturnValue('Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X)');

    expect(TestBed.inject(InstallPromptService).showIosHint()).toBe(true);
  });
});
