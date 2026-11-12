import type { PushEnvironment } from './push-environment';

export const environment: { production: boolean; apiUrl: string; openRouteServiceKey: string } & PushEnvironment = {
  production: true,
  apiUrl: '',
  // Publiczne dane projektu Firebase (nie są sekretem) – te same co w environment.ts.
  firebase: {
    apiKey: 'AIzaSyCaeiUisElRFnYikgwzPz9hH-MnKJq64t4',
    authDomain: 'icanhelp-u.firebaseapp.com',
    projectId: 'icanhelp-u',
    storageBucket: 'icanhelp-u.firebasestorage.app',
    messagingSenderId: '604489494523',
    appId: '1:604489494523:web:d5325fc86895a700f1a55e',
  },
  vapidKey: 'BPbsgep13c7V_MQ37dOvxOq0J63Mfzu1-0lqxCU2LuOF1jDyWaTmAOh1d9RnBKiHp8feUO1H2ztPLmVm1V6m2eY',
  // Klucz OpenRouteService – opis w environment.ts.
  openRouteServiceKey: '',
};
