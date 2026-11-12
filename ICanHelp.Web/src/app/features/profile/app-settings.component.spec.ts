import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { AuthService } from '../../core/auth/auth.service';
import { InstallPromptService } from '../../core/pwa/install-prompt.service';
import type { PushState } from '../../core/pwa/push.service';
import { PushService } from '../../core/pwa/push.service';

import { AppSettingsComponent } from './app-settings.component';

describe('AppSettingsComponent', () => {
  const authenticated = signal(true);
  const install = {
    canPrompt: signal(false),
    showIosHint: signal(false),
    install: vi.fn(() => Promise.resolve(true)),
  };
  const push = { state: signal<PushState>('default'), busy: signal(false), enable: vi.fn(() => Promise.resolve(true)) };

  function render(): HTMLElement {
    TestBed.configureTestingModule({
      imports: [AppSettingsComponent],
      providers: [
        { provide: AuthService, useValue: { isAuthenticated: () => authenticated() } },
        { provide: InstallPromptService, useValue: install },
        { provide: PushService, useValue: push },
      ],
    });
    const fixture = TestBed.createComponent(AppSettingsComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(() => {
    vi.clearAllMocks();
    authenticated.set(true);
    install.canPrompt.set(false);
    install.showIosHint.set(false);
    push.state.set('default');
  });

  it('enables push on click', () => {
    const element = render();
    expect(element.querySelector('[data-testid="push-note"]')?.textContent).toContain('Wyłączone');

    (element.querySelector('[data-testid="enable-push"]') as HTMLElement).click();

    expect(push.enable).toHaveBeenCalled();
  });

  it('shows the state without a button once push is decided', () => {
    push.state.set('enabled');
    const element = render();

    expect(element.querySelector('[data-testid="push-note"]')?.textContent).toContain('Włączone');
    expect(element.querySelector('[data-testid="enable-push"]')).toBeNull();
  });

  it('offers installation on Android and the instruction on an iPhone', () => {
    install.canPrompt.set(true);
    const android = render();
    (android.querySelector('[data-testid="install"]') as HTMLElement).click();
    expect(install.install).toHaveBeenCalled();
    TestBed.resetTestingModule();

    install.canPrompt.set(false);
    install.showIosHint.set(true);
    push.state.set('needs-install');
    const iphone = render();
    expect(iphone.querySelector('[data-testid="ios-hint"]')).not.toBeNull();
    expect(iphone.querySelector('[data-testid="push-note"]')?.textContent).toContain('ekranu początkowego');
  });

  it('renders nothing when push is unavailable and there is nothing to install', () => {
    push.state.set('unavailable');
    expect(render().querySelector('ion-list')).toBeNull();
  });

  it('hides push for a signed-out user', () => {
    authenticated.set(false);
    const element = render();
    expect(element.querySelector('[data-testid="push-note"]')).toBeNull();
  });
});
