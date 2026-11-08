import type { ApplicationConfig } from '@angular/core';
import {
  PreloadAllModules,
  RouteReuseStrategy,
  provideRouter,
  withComponentInputBinding,
  withPreloading,
} from '@angular/router';
import { IonicRouteStrategy, provideIonicAngular } from '@ionic/angular';

import { routes } from './app.routes';
import { provideAppLocale } from './core/config/locale';

export const appConfig: ApplicationConfig = {
  providers: [
    { provide: RouteReuseStrategy, useClass: IonicRouteStrategy },
    provideIonicAngular({ backButtonText: 'Wstecz' }),
    provideRouter(routes, withPreloading(PreloadAllModules), withComponentInputBinding()),
    provideAppLocale(),
  ],
};
