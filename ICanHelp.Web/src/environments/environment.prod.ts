import type { PushEnvironment } from './push-environment';

export const environment: { production: boolean; apiUrl: string } & PushEnvironment = {
  production: true,
  apiUrl: '',
  // Publiczne dane projektu Firebase (nie są sekretem) – te same co w environment.ts.
  firebase: {
    apiKey: '',
    authDomain: '',
    projectId: '',
    storageBucket: '',
    messagingSenderId: '',
    appId: '',
  },
  vapidKey: '',
};
