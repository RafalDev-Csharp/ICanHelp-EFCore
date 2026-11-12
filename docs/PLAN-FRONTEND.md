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

- [x] `ICanHelp.Web/`: projekt Ionic + Angular (standalone, szablon `tabs`), TypeScript strict, ESLint + Prettier.
- [x] Zakładki: **Ogłoszenia**, **Dodaj**, **Rozmowy**, **Powiadomienia**, **Profil** (na razie puste strony).
- [x] Struktura: `src/app/core/` (auth, api, interceptory, konfiguracja), `src/app/shared/` (komponenty wspólne, pipe'y: cena, odległość,
      data „2 h temu”), `src/app/features/<obszar>/` (strony i serwisy obszaru: announcements, offers, conversations, notifications,
      alerts, profile). Język interfejsu: polski (`pl-PL` dla dat i walut).
- [x] Proxy w dev (`proxy.conf.json`): `/api` i `/hubs` (z WebSocketami) → `http://localhost:5080` – bez CORS w trakcie pracy.
      CORS w backendzie dopiero, gdy frontend i API będą na różnych domenach (F10).
- [x] Skrypty npm: `start`, `build`, `test`, `lint`, `api` (ostatni dodaje F2).
- [x] CI: w `.github/workflows/ci.yml` nowy job `frontend` (`npm ci`, `lint`, `test --watch=false`, `build`).
- [x] `ICanHelp.Web/CLAUDE.md` (krótki: stack, struktura, komendy, zasady z „Dlaczego tak”) + wzmianka w głównym CLAUDE.md
      („Gdzie co leży”, „Komendy”).

Zrobione 8.11.2026 (szczegóły: `docs/HISTORIA-KROKOW.md`). Szablon dał też Vitest (testy jednostkowe) i Playwright (e2e) –
zostają; `npm test` = jednostkowe, `npm run test:e2e` = Playwright lokalnie. Skrypt `api` dochodzi w F2.

## Krok F2 – klient API, logowanie i rejestracja

- [x] NSwag: `nswag.json` (wejście: `http://localhost:5080/swagger/v1/swagger.json`, wyjście: `src/app/core/api/api-client.ts`,
      szablon Angular, `HttpClient`, daty jako `string`), `npm run api`. Wygenerowany plik w repo (CI nie musi uruchamiać API).
- [x] `AuthService` (sygnał `currentUser`, token w `localStorage`), interceptor `Authorization: Bearer`, guard tras wymagających logowania.
      Przeglądanie ogłoszeń działa bez logowania (API pozwala) – logowanie dopiero przy akcji („Złóż ofertę” → ekran logowania → powrót).
- [x] Ekrany: logowanie, rejestracja (walidacja jak w backendzie), profil (`GET api/users/me`, wylogowanie).
- [x] Interceptor błędów (ProblemDetails → toast po polsku, `errorCode` → tekst).
- [x] **Backend:** token wygasa po 1 h (`Auth:Expiry`) – na telefonie to wylogowanie co godzinę. Dodać odświeżanie:
      `POST api/users/refresh` + tabela `RefreshTokens` (hash, UserId, ExpiresAt, RevokedAt, rotacja przy każdym użyciu, ważny np. 30 dni),
      `POST api/users/sign-out` (unieważnienie). Testy integracyjne. Frontend odświeża przy 401 (jedno odświeżenie naraz).
- [x] Testy: `AuthService`, interceptory (Vitest – co dał szablon), guard, ekrany logowania i rejestracji, e2e przekierowania na logowanie.

Zrobione 8.11.2026 (szczegóły: `docs/HISTORIA-KROKOW.md`). Nazwy klientów z operationId `Kontroler_Akcja` (backend `CustomOperationIds`),
DTO jako interfejsy. Zakładki Dodaj / Rozmowy / Powiadomienia za `authGuard`, Profil bez logowania pokazuje „Zaloguj się”.

## Krok F3 – przeglądanie ogłoszeń

- [x] Lista: `GET api/announcements` ze stronicowaniem (przewijanie bez końca), filtry: kategoria/podkategoria, status `Open`,
      „blisko mnie” (geolokalizacja **po kliknięciu**, potem `latitude/longitude/radiusKm`), odległość na karcie.
- [x] Karta ogłoszenia: miniatura (`GetThumbnail`), tytuł, budżet, odległość, kategoria. Liczba pomocników (`HelperCount`) – na razie
      tylko w szczegółach, bo lista (`AnnouncementDto`) jej nie ma → zadanie backendowe niżej.
- [x] Szczegóły: galeria zdjęć, opis, mapa z punktem (Leaflet), oceny właściciela, przycisk „Złóż ofertę” / „Napisz”
      (gość → logowanie i powrót; po zalogowaniu na razie komunikat – formularz oferty i wątek w F5).
- [x] Widok mapy listy (przełącznik lista ↔ mapa, pinezki z ogłoszeniami w okolicy).
- [x] Polubienia: API ma tylko odczyt (`GET api/users/{id}/liked-announcements`, `liked-users`), nie da się polubić → bez UI,
      zadanie backendowe niżej.

Zrobione 8.11.2026 (szczegóły: `docs/HISTORIA-KROKOW.md`). Mapa: `shared/components/map` (`app-map`), kafelki i środek domyślny
w `core/config/map.ts` (Augustów; dostawca kafelków z konfiguracji – F10). Pozycja „blisko mnie” zaokrąglana do ~100 m i trzymana
tylko w filtrze ekranu.

**Zadania backendowe z F3** – zrobione w F4 (9.11.2026):
- [x] `HelperCount` w `AnnouncementDto` (lista i alerty `/preview`) – karta na liście pokazuje „N ofert”.
- [x] Polubienia: `PUT` / `DELETE api/announcements/{id}/like` (zalogowany), `IsLiked` dla zalogowanego w `AnnouncementDetailsDto`
  (na liście nie – karty biorą stan z `LikesService`), „Polubione” w profilu z `liked-announcements` + serduszko na karcie i w szczegółach.

## Krok F4 – dodawanie i edycja ogłoszenia

- [x] Formularz: kategoria → podkategoria, trudność, tytuł, opis, budżet; lokalizacja: „moja pozycja” (GPS) / wskazanie na mapie /
      wyszukanie adresu (Nominatim).
- [x] Zdjęcia (max 5): `<input type="file" accept="image/*" capture>` – na telefonie otwiera aparat; zmniejszanie przed wysłaniem
      (canvas, np. dłuższy bok 1600 px, JPEG) – szybszy upload na komórkowym internecie.
- [x] „Moje ogłoszenia” (filtr po właścicielu), edycja (`PATCH`), obsługa 409 `ConcurrencyConflict` („ktoś zmienił w międzyczasie,
      odśwież”), anulowanie.

Zrobione 9.11.2026 (szczegóły: `docs/HISTORIA-KROKOW.md`). Decyzje: miejsce ogłoszenia zaokrąglane do ~100 m (dokładny adres –
po akceptacji, w kontakcie); dwa przyciski zdjęć – „Zrób zdjęcie” (`capture`, sam aparat) i „Z galerii” (bez `capture`, kilka naraz);
PATCH zawsze z `expectedRevision` (nowe pole backendu – bez niego 409 przy edycji praktycznie nie występował), także przy samych
zmianach zdjęć (pusty PATCH nie tworzy rewizji, tylko sprawdza wersję); edycja tylko w statusie `Open`, anulowanie w szczegółach.
„Polubione” dociąga ogłoszenia po jednym (max 50) – przy większej skali endpoint zwracający od razu `AnnouncementDto`.

## Krok F5 – oferty, negocjacje, realizacja, oceny

- [x] Złożenie oferty (cena, termin od–do, wiadomość), kontroferta, akceptacja/odrzucenie (druga strona), wycofanie (autor).
      Oferta nieaktualna (`IsOutdated`) – wyraźne oznaczenie, zamiast „Akceptuj” → „Zaproponuj kontrofertę”.
- [x] Wątek rozmowy (`api/conversations/{id}`): wiadomości, wysyłanie, oznaczanie przeczytanych; tylko do odczytu po zakończeniu.
- [x] Zakładka **Rozmowy** = `GET api/me/conversations` (role Klient / Pomocnik, nieprzeczytane).
- [x] Po akceptacji: dane kontaktowe (`/contact`), Start / Zakończ / Anuluj zgodnie z uprawnieniami, ocena po `Completed`.
- [x] Oś czasu ogłoszenia dla właściciela: oferty, kontroferty, zmiany (rewizje).

Zrobione 9.11.2026 (szczegóły: `docs/HISTORIA-KROKOW.md`). Decyzje: akcje na ofertach (przyjmij / odrzuć / kontroferta / wycofaj)
są **w wątku** – tam widać całą negocjację z jednym pomocnikiem; szczegóły ogłoszenia pokazują listę negocjacji (link do wątku)
i „Historię ogłoszenia” (właściciel). Oferta / kontroferta / ocena w oknach (Ionic modal), nieodwracalne kroki z potwierdzeniem.
Pomocnik z oczekującą ofertą zamiast „Złóż ofertę” widzi „Twoja oferta: X zł” (→ wątek; backend i tak odrzuca drugą oczekującą).
Termin oferty: `<input type="datetime-local">` (natywny wybór daty na telefonie), domyślnie najbliższa pełna godzina za ≥ 1 h, na 2 h.
Backend: filtr `announcementId` w `GET api/me/conversations` – właściciel nie mógł inaczej trafić ze szczegółów do wątku pomocnika.
Nowe wiadomości bez odświeżania – F6 (SignalR); do tego czasu „pociągnij, aby odświeżyć”.

## Krok F6 – powiadomienia w aplikacji (SignalR)

- [x] `@microsoft/signalr`, połączenie z `/hubs/notifications` (token w `accessTokenFactory`), automatyczne wznawianie.
- [x] Licznik nieprzeczytanych na zakładce (`unread-count` + zdarzenia z huba), lista, „oznacz wszystkie”, toast przy nowym.
- [x] Teksty powiadomień z `Type` + `Payload` (albo z backendu, jeśli Krok 15 doda `Title`/`Body` do `NotificationDto` – wtedy jedno źródło).
- [x] Kliknięcie → właściwy ekran (ogłoszenie, wątek). Nowa wiadomość w otwartym wątku – dopisana od razu.

Zrobione 9.11.2026 (szczegóły: `docs/HISTORIA-KROKOW.md`). Decyzje: serwis w `core/notifications/` (używają go zakładki, wątek,
lista rozmów), łączy się tylko zalogowany, hub tylko przyspiesza – licznik zawsze z `unread-count` (po zalogowaniu, wznowieniu,
powrocie do karty). Teksty w jednym pliku `notification-texts.ts` – przy Kroku 15 (push potrzebuje tekstu na serwerze) rozważyć
`Title`/`Body` w `NotificationDto` i usunięcie tekstów z frontu. (Krok 15: zrobione – API zwraca `title`/`body`/`url`, front je
bierze; teksty z `parameters` zostały jako zapas.) Oferty i wiadomości prowadzą do wątku (tam akcje), edycja i alert –
do ogłoszenia. Nowa wiadomość w otwartym wątku = przeładowanie wątku z API (kolejność i godziny z serwera), bez toastu, od razu
przeczytana. Backend: `read-all?conversationId=` – wejście do wątku gasi jego powiadomienia.

## Krok F7 – PWA i push (po backendowym Kroku 15)

- [x] `ng add @angular/pwa`: manifest (nazwa, ikony, kolory, `display: standalone`), service worker, praca offline dla „skorupy” aplikacji.
- [x] Zachęta do instalacji: na Androidzie `beforeinstallprompt` („Zainstaluj aplikację”), na iPhonie krótka instrukcja
      („Udostępnij → Do ekranu początkowego”) – bez tego iPhone nie dostanie pusha.
- [x] Firebase Messaging (JS SDK): zgoda na powiadomienia **po kliknięciu** (np. po złożeniu pierwszej oferty: „Powiadomić Cię
      o odpowiedzi?”), `getToken` → `POST api/me/devices`, przy wylogowaniu `DELETE`.
      Uwaga: Angular ma swój service worker (`ngsw`), Firebase chce swojego – użyć jednego (własny SW importujący `ngsw-worker.js`
      i obsługę FCM, rejestracja przekazana do `getToken({ serviceWorkerRegistration })`). Sprawdzić przy implementacji.
- [x] Kliknięcie w push → otwarcie aplikacji na `data.url`. W otwartej aplikacji – toast zamiast systemowego dymka.
- [x] Test lokalny przez HTTPS (12.11.2026, F10): `serve-local.cmd` → https://localhost:7080 – SW, offline, instalowalność,
      push przy otwartej i zamkniętej aplikacji w Chrome (szczegóły: `docs/HISTORIA-KROKOW.md`, Krok F10).
- [ ] Test ręczny: Android (Chrome) i iPhone (PWA z ekranu głównego) przez tunel HTTPS na port 5080
      (`devtunnel host -p 5080 --allow-anonymous` / `ngrok http 5080`), kliknięcie w dymek → właściwy wątek.

Zrobione 10.11.2026 (szczegóły: `docs/HISTORIA-KROKOW.md`). Decyzje: jeden service worker `public/app-sw.js` – sam obsługuje
`push` i `notificationclick` (bez SDK Firebase w workerze), rejestruje słuchacze **przed** `importScripts('./ngsw-worker.js')`
i zatrzymuje ngsw (`stopImmediatePropagation`), bo ngsw pokazałby dymek także przy otwartej aplikacji. Aplikacja widoczna →
worker przekazuje push do strony (`postMessage`) → toast z `NotificationsService`; to samo powiadomienie z huba i z pusha
(id) pokazujemy raz. Kliknięcie: otwarta karta → `focus` + router (bez przeładowania), inaczej `openWindow`; link spoza
aplikacji → start. Zgoda: okno „Powiadomić Cię…?” po złożeniu oferty i dodaniu ogłoszenia (najwyżej raz na tydzień) albo
„Włącz” w profilu (sekcja „Aplikacja”); przy zgodzie już udzielonej token zapisujemy przy każdym starcie (backend robi upsert).
Wylogowanie: `DELETE api/me/devices/{id}` przed końcem sesji + unieważnienie tokenu; sesja utracona bez wylogowania – samo
unieważnienie (FCM odrzuci token, backend usunie urządzenie). Firebase SDK ładowany leniwie (osobny chunk). W `ng serve` service
worker jest wyłączony – push testujemy na `npm run build` (np. tunel HTTPS do serwera statycznego `www/`). Ikony: własne
(białe serce na `#0054e9`), maskable osobno; `navigationUrls` pomija `/api`, `/hubs`, `/swagger`, `/health` (F10: jedna domena).

## Krok F8 – alerty („w okolicy” i „po drodze”)

- [x] Lista alertów (`api/me/alerts`), włącz/wyłącz, usuń.
- [x] Alert obszaru: punkt na mapie / moja pozycja / adres, suwak promienia (0,5–50 km, okrąg na mapie), kategoria + podkategorie,
      dni i godziny.
- [x] Alert trasy: start i cel (adres / mapa) → trasa z OpenRouteService → polilinia (pas o szerokości promienia na mapie) → zapis.
      Klucz ORS w konfiguracji frontendu (darmowy limit wystarczy; przy wzroście – Wariant 2 z backendem, Krok 14).
- [x] Podgląd: „pasujące teraz” (`/preview`).
- [ ] Ręcznie: klucz ORS w `environment*.ts` (`openRouteServiceKey`, autor) i sprawdzenie trasy na żywo.

Zrobione 12.11.2026 (szczegóły: `docs/HISTORIA-KROKOW.md`). Decyzje: alerty w profilu (`/profile/alerts`, nowy:
`/profile/alerts/new?kind=Area|Route`, edycja + podgląd: `/profile/alerts/:alertId`) – to ustawienie, nie osobna zakładka.
Przełącznik na liście działa od razu (`enable`/`disable`, przy błędzie wraca), usuwanie – przesunięcie albo przycisk w edycji,
zawsze z potwierdzeniem; przy 10 alertach przyciski „Nowy” znikają (backend i tak da 409). Formularz na sygnałach (bez
Reactive Forms – dni jako flagi, mapa, trasa). Dni: skróty „Codziennie / Dni robocze / Weekend” + Pn–Nd (flagi `WeekDays`
jak w backendzie, np. „Monday, Friday”), godziny opcjonalne (`type="time"`, przez noc OK). Trasa: segment Start / Cel, adres /
moja pozycja / dotknięcie mapy, droga liczy się sama, gdy są oba punkty (`POST` ORS `driving-car/geojson`, klucz w nagłówku, poza
interceptorami); polilinia zaokrąglona do ~1 m, bez powtórzeń i uproszczona (Douglas–Peucker 25 m, max 500 punktów). Bez klucza
– linia prosta z ostrzeżeniem (do pracy lokalnej). Pas na mapie: linia o grubości 2 × promień przeliczanej przy zoomie (okrągłe
końce ≈ bufor z bazy). Wspólne wyszukiwanie miejsca: `shared/components/place-search` (używa go też formularz ogłoszenia).
Podgląd liczy backend dla zapisanego alertu – odświeża się po „Zapisz zmiany”.

## Krok F9 – „Jestem w okolicy” (po backendowym Kroku 16)

- [ ] Duży przycisk (np. na zakładce Ogłoszenia): „Jestem w okolicy” → czas (1/2/4 h), promień, kategorie (zapamiętane z ostatniego razu)
      → geolokalizacja → `POST api/me/presence` → lista pasujących ogłoszeń. Pasek „Dostępny do 18:30 · Zakończ”.
- [ ] Przy alertach tras: „Wracam teraz” (`activate-now`).
- [ ] Odświeżanie pozycji tylko przy otwartej aplikacji (`watchPosition`, wysyłka co ≥ 1 min i przy ruchu > 200 m).
- [ ] Dla zleceniodawcy na jego ogłoszeniu: „W okolicy jest teraz N pomocników” (jeśli backend to zrobi).

## Krok F10 – wdrożenie i wymagania przed testem z ludźmi

- [x] Frontend serwowany przez API (`wwwroot` / `Frontend:Path`, fallback na `index.html`, nagłówki cache dla workera) →
      jedna domena, bez CORS. Lokalnie: `serve-local.cmd` (12.11.2026).
- [ ] Hosting z HTTPS: API (np. Azure App Service / VPS z Dockerem) + baza; obraz/publikacja z buildem frontendu w `wwwroot`,
      `Push:Fcm:WebAppUrl` = adres produkcyjny. Alternatywa: Azure Static Web Apps + CORS.
- [ ] Konfiguracja produkcyjna: `Auth:SigningKey`, połączenie z bazą, Firebase, klucz ORS, dostawca kafelków map – z env, nie z repo.
- [ ] Zdjęcia: magazyn trwały (dysk/Blob), kopie zapasowe bazy.
- [ ] Prawne: regulamin, polityka prywatności (lokalizacja, push, e-mail), zgoda przy rejestracji, usunięcie konta (RODO) –
      sprawdzić, czy backend ma usuwanie/anonimizację konta (jeśli nie – zadanie backendowe).
- [ ] Monitoring błędów (np. Sentry, darmowy próg) dla frontu i API.
