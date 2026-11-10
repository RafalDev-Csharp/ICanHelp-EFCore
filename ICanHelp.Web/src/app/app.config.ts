import { provideHttpClient, withInterceptors } from '@angular/common/http';
import type { ApplicationConfig } from '@angular/core';
import { inject, isDevMode, provideAppInitializer } from '@angular/core';
import {
  PreloadAllModules,
  RouteReuseStrategy,
  provideRouter,
  withComponentInputBinding,
  withPreloading,
} from '@angular/router';
import { provideServiceWorker } from '@angular/service-worker';
import { IonicRouteStrategy, provideIonicAngular } from '@ionic/angular';

import { routes } from './app.routes';
import { provideApi } from './core/api/api.providers';
import { provideAppLocale } from './core/config/locale';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { InstallPromptService } from './core/pwa/install-prompt.service';

export const appConfig: ApplicationConfig = {
  providers: [
    { provide: RouteReuseStrategy, useClass: IonicRouteStrategy },
    provideIonicAngular({ backButtonText: 'Wstecz' }),
    provideRouter(routes, withPreloading(PreloadAllModules), withComponentInputBinding()),
    provideAppLocale(),
    // Kolejność: errorInterceptor widzi błąd dopiero po próbie odświeżenia tokenu w authInterceptor.
    provideHttpClient(withInterceptors([errorInterceptor, authInterceptor])),
    provideApi(),
    // Jeden service worker: `public/app-sw.js` = push (FCM) + `ngsw-worker.js` (cache, aktualizacje). W `ng serve` wyłączony.
    provideServiceWorker('app-sw.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000',
    }),
    // `beforeinstallprompt` przychodzi zaraz po wczytaniu – słuchamy od startu.
    provideAppInitializer(() => void inject(InstallPromptService)),
  ],
};
