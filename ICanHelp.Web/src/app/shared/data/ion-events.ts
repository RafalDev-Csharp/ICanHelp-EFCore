/**
 * Ionic: `ion-refresher` i `ion-infinite-scroll` trzeba „zamknąć” (`complete()`) po wczytaniu danych – inaczej
 * wskaźnik kręci się dalej. `event` – zdarzenie `ionRefresh` / `ionInfinite`.
 */
export function completeIonEvent(event: Event): void {
  void (event.target as unknown as { complete(): Promise<void> }).complete();
}
