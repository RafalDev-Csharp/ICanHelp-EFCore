/** Klucz w `localStorage`: zapisane urządzenie (do `DELETE api/me/devices/{id}`) i kiedy ostatnio pytaliśmy o zgodę. */
export const PUSH_STORAGE_KEY = 'icanhelp.push';

/** To, co aplikacja pamięta o pushu na tym urządzeniu (między sesjami). */
export interface StoredPush {
  /** Id urządzenia w API – do usunięcia przy wylogowaniu. */
  deviceId?: string;
  /** Token FCM zapisany w API. */
  token?: string;
  /** Kiedy ostatnio pokazaliśmy zachętę „Powiadomić Cię…?” (ms od epoki). */
  suggestedAt?: number;
}

export function readStoredPush(): StoredPush {
  try {
    const value = JSON.parse(localStorage.getItem(PUSH_STORAGE_KEY) ?? '{}') as StoredPush | null;
    return value && typeof value === 'object' ? value : {};
  } catch {
    return {};
  }
}

export function writeStoredPush(value: StoredPush): void {
  try {
    localStorage.setItem(PUSH_STORAGE_KEY, JSON.stringify(value));
  } catch {
    // tryb prywatny / pełny magazyn – push i tak zadziała do końca tej sesji
  }
}
