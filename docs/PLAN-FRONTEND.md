# Plan: frontend (PWA – Angular + Ionic, później Capacitor)

> Plan na Kroki F1–F10. Każdy krok to osobna sesja: „Przeczytaj CLAUDE.md i docs/PLAN-FRONTEND.md, zrób Krok FN”.
> Po kroku: odhacz punkty tutaj, zaktualizuj „Stan prac” w CLAUDE.md, `npm run lint` + `npm test` + `npm run build`
> (a gdy krok rusza backend – też `dotnet build` + `dotnet test`), commit.
> Kroki backendu 15–17 (push, „jestem w okolicy”, sklepy): `docs/PLAN-POWIADOMIENIA.md`. Decyzja o kliencie – tamże, „Klient i kanał push”.

## Kolejność całości (frontend + backend)

```
F1 szkielet → F2 logowanie → F3 przeglądanie → F4 dodawanie → F5 oferty i rozmowy → F6 powiadomienia w aplikacji
→ Krok 15 (backend: push) → F7 PWA + push → F8 alerty
→ F10 wdrożenie  ══►  MVP: test w jednym mieście (Augustów / Suwałki)
→ Krok 16 (backend: „jestem w okolicy”) → F9 „jestem w okolicy” → Krok 17 (Capacitor, sklepy)
```
F10 (wdrożenie) można zrobić wcześniej, jeśli chcesz pokazać aplikację znajomym już po F5.

## Dlaczego tak (decyzje, których nie zmieniamy bez powodu)

1. **Angular + Ionic + PWA, później Capacitor.** Angular przypomina .NET (TypeScript, DI, serwisy, jedna struktura),
   Ionic daje komponenty wyglądające jak aplikacja mobilna, PWA – instalację na ekranie głównym i push, Capacitor (Krok 17) –
   aplikację do sklepów z tego samego kodu. Pisane „mobile first”: najpierw telefon, komputer ma działać, ale nie jest priorytetem.
2. **Ten sam repozytorium, folder `ICanHelp.Web/`.** Backend i frontend zmieniają się razem (nowy endpoint + ekran w jednym
   commicie), CI w jednym miejscu. Folder dostaje własny `ICanHelp.Web/CLAUDE.md` (stack, struktura, komendy frontendu) –
   główny CLAUDE.md zostaje krótki, a sesja frontendowa czyta oba.
3. **Klient API generowany ze Swaggera (NSwag), nie pisany ręcznie.** Typy DTO i metody w TypeScripcie zawsze zgodne z backendem;
   po zmianie API: `npm run api` (regeneracja) i kompilator pokazuje, co poprawić. Wygenerowanego pliku nie edytujemy.
4. **Nowoczesny Angular:** standalone components (bez `NgModule`), sygnały (`signal`, `computed`) do stanu, `inject()`,
   lazy loading tras. Bez NgRx – na tę skalę wystarczą serwisy z sygnałami.
5. **Błędy z backendu = ProblemDetails z `errorCode`.** Jeden interceptor tłumaczy `errorCode` na polski komunikat (mapa
   `errorCode → tekst`, domyślnie `title`), 401 → wylogowanie / odświeżenie tokenu. Ekrany nie parsują błędów same.
6. **Mapy: Leaflet + OpenStreetMap** (darmowe, bez klucza). Kafelki OSM mają limity użycia – na produkcji dostawca kafelków
   (np. MapTiler, darmowy próg) – w F10. Wyszukiwanie adresu: Nominatim (limit 1 zapytanie/s, wymaga własnego User-Agent/Referer).
   Trasa dla alertów: OpenRouteService (darmowy klucz) – zgodnie z „Wariant 1” w Kroku 14 (frontend liczy trasę, backend dostaje polilinię).
7. **HTTPS jest wymagany** dla PWA, geolokalizacji i pusha (wyjątek: `localhost`). Test na telefonie w trakcie pracy –
   tunel (Dev Tunnels z Visual Studio albo `ngrok`).
8. **Prywatność w UI jak w API:** pozycji użytkownika nie wysyłamy bez akcji użytkownika; dokładna pozycja nigdy nie jest pokazywana innym.

## Przygotowanie (raz, ręcznie)

- Node.js LTS (nodejs.org), potem `npm i -g @angular/cli @ionic/cli`.
- VS Code + rozszerzenie „Angular Language Service” (Visual Studio do backendu zostaje).
- Konto Firebase (dopiero przy F7), klucz OpenRouteService (dopiero przy F8).

## Krok F1 – szkielet aplikacji

- [ ] `ICanHelp.Web/`: projekt Ionic + Angular (standalone, szablon `tabs`), TypeScript strict, ESLint + Prettier.
- [ ] Zakładki: **Ogłoszenia**, **Dodaj**, **Rozmowy**, **Powiadomienia**, **Profil** (na razie puste strony).
- [ ] Struktura: `src/app/core/` (auth, api, interceptory, konfiguracja), `src/app/shared/` (komponenty wspólne, pipe'y: cena, odległość,
      data „2 h temu”), `src/app/features/<obszar>/` (strony i serwisy obszaru: announcements, offers, conversations, notifications,
      alerts, profile). Język interfejsu: polski (`pl-PL` dla dat i walut).
- [ ] Proxy w dev (`proxy.conf.json`): `/api` i `/hubs` (z WebSocketami) → `http://localhost:5080` – bez CORS w trakcie pracy.
      CORS w backendzie dopiero, gdy frontend i API będą na różnych domenach (F10).
- [ ] Skrypty npm: `start`, `build`, `test`, `lint`, `api` (ostatni dodaje F2).
- [ ] CI: w `.github/workflows/ci.yml` nowy job `frontend` (`npm ci`, `lint`, `test --watch=false`, `build`).
- [ ] `ICanHelp.Web/CLAUDE.md` (krótki: stack, struktura, komendy, zasady z „Dlaczego tak”) + wzmianka w głównym CLAUDE.md
      („Gdzie co leży”, „Komendy”).

## Krok F2 – klient API, logowanie i rejestracja

- [ ] NSwag: `nswag.json` (wejście: `http://localhost:5080/swagger/v1/swagger.json`, wyjście: `src/app/core/api/api-client.ts`,
      szablon Angular, `HttpClient`, daty jako `string`), `npm run api`. Wygenerowany plik w repo (CI nie musi uruchamiać API).
- [ ] `AuthService` (sygnał `currentUser`, token w `localStorage`), interceptor `Authorization: Bearer`, guard tras wymagających logowania.
      Przeglądanie ogłoszeń działa bez logowania (API pozwala) – logowanie dopiero przy akcji („Złóż ofertę” → ekran logowania → powrót).
- [ ] Ekrany: logowanie, rejestracja (walidacja jak w backendzie), profil (`GET api/users/me`, wylogowanie).
- [ ] Interceptor błędów (ProblemDetails → toast po polsku, `errorCode` → tekst).
- [ ] **Backend:** token wygasa po 1 h (`Auth:Expiry`) – na telefonie to wylogowanie co godzinę. Dodać odświeżanie:
      `POST api/users/refresh` + tabela `RefreshTokens` (hash, UserId, ExpiresAt, RevokedAt, rotacja przy każdym użyciu, ważny np. 30 dni),
      `POST api/users/sign-out` (unieważnienie). Testy integracyjne. Frontend odświeża przy 401 (jedno odświeżenie naraz).
- [ ] Testy: `AuthService`, interceptory (Jasmine/Karma albo Jest – co da szablon).

## Krok F3 – przeglądanie ogłoszeń

- [ ] Lista: `GET api/announcements` ze stronicowaniem (przewijanie bez końca), filtry: kategoria/podkategoria, status `Open`,
      „blisko mnie” (geolokalizacja **po kliknięciu**, potem `latitude/longitude/radiusKm`), odległość na karcie.
- [ ] Karta ogłoszenia: miniatura (`GetThumbnail`), tytuł, budżet, odległość, kategoria, liczba pomocników (`HelperCount`).
- [ ] Szczegóły: galeria zdjęć, opis, mapa z punktem (Leaflet), oceny właściciela, przycisk „Złóż ofertę” / „Napisz”.
- [ ] Widok mapy listy (przełącznik lista ↔ mapa, pinezki z ogłoszeniami w okolicy).
- [ ] Polubienia (jeśli API je wystawia – sprawdzić `LikedAnnouncement`; brakujące endpointy zapisz jako zadanie backendowe).

## Krok F4 – dodawanie i edycja ogłoszenia

- [ ] Formularz: kategoria → podkategoria, trudność, tytuł, opis, budżet; lokalizacja: „moja pozycja” (GPS) / wskazanie na mapie /
      wyszukanie adresu (Nominatim).
- [ ] Zdjęcia (max 5): `<input type="file" accept="image/*" capture>` – na telefonie otwiera aparat; zmniejszanie przed wysłaniem
      (canvas, np. dłuższy bok 1600 px, JPEG) – szybszy upload na komórkowym internecie.
- [ ] „Moje ogłoszenia” (filtr po właścicielu), edycja (`PATCH`), obsługa 409 `ConcurrencyConflict` („ktoś zmienił w międzyczasie,
      odśwież”), anulowanie.

## Krok F5 – oferty, negocjacje, realizacja, oceny

- [ ] Złożenie oferty (cena, termin od–do, wiadomość), kontroferta, akceptacja/odrzucenie (druga strona), wycofanie (autor).
      Oferta nieaktualna (`IsOutdated`) – wyraźne oznaczenie, zamiast „Akceptuj” → „Zaproponuj kontrofertę”.
- [ ] Wątek rozmowy (`api/conversations/{id}`): wiadomości, wysyłanie, oznaczanie przeczytanych; tylko do odczytu po zakończeniu.
- [ ] Zakładka **Rozmowy** = `GET api/me/conversations` (role Klient / Pomocnik, nieprzeczytane).
- [ ] Po akceptacji: dane kontaktowe (`/contact`), Start / Zakończ / Anuluj zgodnie z uprawnieniami, ocena po `Completed`.
- [ ] Oś czasu ogłoszenia dla właściciela: oferty, kontroferty, zmiany (rewizje).

## Krok F6 – powiadomienia w aplikacji (SignalR)

- [ ] `@microsoft/signalr`, połączenie z `/hubs/notifications` (token w `accessTokenFactory`), automatyczne wznawianie.
- [ ] Licznik nieprzeczytanych na zakładce (`unread-count` + zdarzenia z huba), lista, „oznacz wszystkie”, toast przy nowym.
- [ ] Teksty powiadomień z `Type` + `Payload` (albo z backendu, jeśli Krok 15 doda `Title`/`Body` do `NotificationDto` – wtedy jedno źródło).
- [ ] Kliknięcie → właściwy ekran (ogłoszenie, wątek). Nowa wiadomość w otwartym wątku – dopisana od razu.

## Krok F7 – PWA i push (po backendowym Kroku 15)

- [ ] `ng add @angular/pwa`: manifest (nazwa, ikony, kolory, `display: standalone`), service worker, praca offline dla „skorupy” aplikacji.
- [ ] Zachęta do instalacji: na Androidzie `beforeinstallprompt` („Zainstaluj aplikację”), na iPhonie krótka instrukcja
      („Udostępnij → Do ekranu początkowego”) – bez tego iPhone nie dostanie pusha.
- [ ] Firebase Messaging (JS SDK): zgoda na powiadomienia **po kliknięciu** (np. po złożeniu pierwszej oferty: „Powiadomić Cię
      o odpowiedzi?”), `getToken` → `POST api/me/devices`, przy wylogowaniu `DELETE`.
      Uwaga: Angular ma swój service worker (`ngsw`), Firebase chce swojego – użyć jednego (własny SW importujący `ngsw-worker.js`
      i obsługę FCM, rejestracja przekazana do `getToken({ serviceWorkerRegistration })`). Sprawdzić przy implementacji.
- [ ] Kliknięcie w push → otwarcie aplikacji na `data.url`. W otwartej aplikacji – toast zamiast systemowego dymka.
- [ ] Test ręczny: Android (Chrome) i iPhone (PWA z ekranu głównego) przez tunel HTTPS. Lighthouse (kategoria PWA).

## Krok F8 – alerty („w okolicy” i „po drodze”)

- [ ] Lista alertów (`api/me/alerts`), włącz/wyłącz, usuń.
- [ ] Alert obszaru: punkt na mapie / moja pozycja / adres, suwak promienia (0,5–50 km, okrąg na mapie), kategoria + podkategorie,
      dni i godziny.
- [ ] Alert trasy: start i cel (adres / mapa) → trasa z OpenRouteService → polilinia (pas o szerokości promienia na mapie) → zapis.
      Klucz ORS w konfiguracji frontendu (darmowy limit wystarczy; przy wzroście – Wariant 2 z backendem, Krok 14).
- [ ] Podgląd: „pasujące teraz” (`/preview`).

## Krok F9 – „Jestem w okolicy” (po backendowym Kroku 16)

- [ ] Duży przycisk (np. na zakładce Ogłoszenia): „Jestem w okolicy” → czas (1/2/4 h), promień, kategorie (zapamiętane z ostatniego razu)
      → geolokalizacja → `POST api/me/presence` → lista pasujących ogłoszeń. Pasek „Dostępny do 18:30 · Zakończ”.
- [ ] Przy alertach tras: „Wracam teraz” (`activate-now`).
- [ ] Odświeżanie pozycji tylko przy otwartej aplikacji (`watchPosition`, wysyłka co ≥ 1 min i przy ruchu > 200 m).
- [ ] Dla zleceniodawcy na jego ogłoszeniu: „W okolicy jest teraz N pomocników” (jeśli backend to zrobi).

## Krok F10 – wdrożenie i wymagania przed testem z ludźmi

- [ ] Hosting z HTTPS: API (np. Azure App Service / VPS z Dockerem) + baza; frontend jako pliki statyczne – najprościej serwowane
      przez to samo API (`wwwroot`, fallback na `index.html`) → jedna domena, bez CORS. Alternatywa: Azure Static Web Apps + CORS.
- [ ] Konfiguracja produkcyjna: `Auth:SigningKey`, połączenie z bazą, Firebase, klucz ORS, dostawca kafelków map – z env, nie z repo.
- [ ] Zdjęcia: magazyn trwały (dysk/Blob), kopie zapasowe bazy.
- [ ] Prawne: regulamin, polityka prywatności (lokalizacja, push, e-mail), zgoda przy rejestracji, usunięcie konta (RODO) –
      sprawdzić, czy backend ma usuwanie/anonimizację konta (jeśli nie – zadanie backendowe).
- [ ] Monitoring błędów (np. Sentry, darmowy próg) dla frontu i API.
