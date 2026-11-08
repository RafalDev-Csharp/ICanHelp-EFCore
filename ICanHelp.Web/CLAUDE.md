# ICanHelp.Web – frontend (kontekst dla asystenta AI)

> Czytaj razem z głównym `../CLAUDE.md` (domena, API, zasady backendu). Plan kroków: `../docs/PLAN-FRONTEND.md`.
> Trzymaj plik krótki – stan prac prowadzimy w głównym CLAUDE.md i w planie.

## Stack
- Angular 22 (standalone, sygnały, `inject()`, lazy loading tras), Ionic 9, TypeScript 6 strict.
- Wygenerowany `ionic start icanhelp-web tabs --type=angular-standalone` (8.11.2026). Komponenty Ionic importujemy z `@ionic/angular`.
- Testy: jednostkowe Vitest (`*.spec.ts` obok kodu, `describe/it/expect` globalnie), e2e Playwright (`e2e/`, telefon Pixel 7).
- Lint: ESLint (`@ionic/eslint-config` + `angular-eslint`) + Prettier (`@ionic/prettier-config`: 120 znaków, `'`, przecinki na końcu).
- Capacitor jest w zależnościach z szablonu, ale platformy (android/ios) dochodzą dopiero w Kroku 17.

## Struktura `src/app/`
```
app.config.ts     providery (router, Ionic, locale) · app.routes.ts → tabs/tabs.routes.ts (zakładki bez prefiksu: /announcements, /add,
                  /conversations, /notifications, /profile; nieznany adres → /announcements)
core/             jedno miejsce na całą aplikację: config/locale.ts (pl-PL, PLN); od F2: api/ (NSwag), auth/ (AuthService, guard),
                  interceptors/ (token, ProblemDetails → toast)
shared/           components/ (EmptyStateComponent), pipes/ (`price`, `distance` – km z API, `timeAgo` – „2 h temu”)
features/<obszar>/ strony i serwisy obszaru: announcements (lista + „Dodaj”), conversations, notifications, profile;
                  offers i alerts dojdą w F5/F8
tabs/             TabsPage – dolny pasek zakładek, rejestruje ikony zakładek (`addIcons`)
```
`src/environments/environment*.ts`: `apiUrl` – pusty = ta sama domena (dev przez proxy, produkcja: API serwuje frontend, F10).

## Zasady (z `docs/PLAN-FRONTEND.md` „Dlaczego tak”)
1. Mobile first – najpierw telefon (Ionic), komputer ma działać.
2. Klient API generowany NSwagiem (`npm run api`, od F2) do `core/api/api-client.ts` – **nie edytujemy** wygenerowanego pliku.
3. Stan w serwisach z sygnałami (`signal`, `computed`), bez NgRx i bez `NgModule`. Zależności przez `inject()`.
4. Błędy API = ProblemDetails z `errorCode` – tłumaczy je jeden interceptor (mapa `errorCode → tekst po polsku`), ekrany nie parsują błędów.
5. Daty z API są w UTC, czasem bez strefy – zawsze przez `parseApiDate` (`shared/pipes/time-ago.pipe.ts`), nie `new Date(str)`.
6. Mapy: Leaflet + OSM; adresy: Nominatim; trasa: OpenRouteService. Pozycja użytkownika tylko po jego akcji, dokładnej nie pokazujemy innym.
7. Interfejs po polsku (`pl-PL`), nazwy w kodzie po angielsku, komentarze mogą być po polsku.
8. Lint wymusza m.in. `import type` dla typów, kolejność importów (zewnętrzne / `../` / `./`, alfabetycznie), typy zwracane
   w publicznych metodach i eksportowanych funkcjach. Nowe strony: `*.page.ts` + `*.page.html`, selektor z prefiksem `app-`.

## Komendy (w folderze `ICanHelp.Web`)
```bash
npm ci                 # instalacja wg package-lock (Node 22 LTS)
npm start              # http://localhost:4200, /api i /hubs → http://localhost:5080 (proxy.conf.json; backend: dotnet run --project ICanHelp.Api)
npm run lint           # ESLint + typecheck e2e + Prettier --check   (npm run lint:fix – poprawia automatycznie)
npm test               # testy jednostkowe (Vitest, jednorazowo)     (npm run test:watch – w trakcie pracy)
npm run test:e2e       # Playwright (pierwszy raz: npx playwright install chromium)
npm run build          # produkcyjny build do www/
```
Test na telefonie: `npm start -- --host 0.0.0.0` w tej samej sieci Wi-Fi, a gdy potrzebny HTTPS (geolokalizacja, PWA, push) –
tunel (Dev Tunnels / ngrok) na port 4200. CI: job `frontend` w `../.github/workflows/ci.yml` (`npm ci`, lint, test, build).
