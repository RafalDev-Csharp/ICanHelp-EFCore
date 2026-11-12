# ICanHelpApp – kontekst projektu dla asystenta AI

> Ten plik czyta agent AI na początku każdej sesji. Trzymaj go krótkim (< 150 linii): szczegóły obszarów → `docs/ARCHITEKTURA.md`,
> pełny opis kroku → `docs/HISTORIA-KROKOW.md`. Po kroku zaktualizuj tylko „Stan prac” (jedna linia).

## Pomysł
Marketplace drobnej pomocy sąsiedzkiej. **Zleceniodawca (Client)** publikuje ogłoszenie z problemem (cieknący kran, malowanie,
porada). **Pomocnik (Helper)**, który zna się na rzeczy i najlepiej przejeżdża w pobliżu, składa ofertę z ceną. Strony negocjują
(kontroferty), zleceniodawca akceptuje → zlecenie → wykonane → wzajemne oceny. Rola to rola w transakcji, nie rola konta.

## Stack
- .NET 9, C# (Nullable), EF Core 9 + SQL Server. Clean Architecture: `Core` (domena) ← `Application` (CQRS, DTO) ←
  `Infrastructure` (EF, repozytoria) ← `Api` (ASP.NET Core). Frontend: `ICanHelp.Web` (Angular + Ionic PWA, własny `CLAUDE.md`).
- Testy: xUnit + Shouldly – `Core.Tests`, `Application.Tests` (ręczne fake'i w `Fakes/`), `Api.Tests` (integracyjne:
  `WebApplicationFactory` + SQL Server z Testcontainers, `[Trait("Category","Integration")]`). Nie FluentAssertions 8+ (licencja płatna).

## Zasady w kodzie (stosuj zawsze)
1. **Core nie zależy od niczego** (żadnego EF, ASP.NET). Tylko czysty C#.
2. Encje: `private set`, prywatny bezparametrowy konstruktor dla EF, publiczny konstruktor przyjmujący **tylko wartości**.
   Części agregatu (`Offer`, `Image`, `Subcategory`) mają konstruktor `internal` – tworzy je korzeń (`announcement.AddOffer(...)`).
3. Kolekcje: `private readonly List<T> _items = new(); public IReadOnlyCollection<T> Items => _items.AsReadOnly();`
4. Logika biznesowa w encjach (np. `announcement.AcceptOffer(offerId)`), nie w handlerach.
5. VO jako `sealed record` z walidacją w konstruktorze. Wyjątki public, dziedziczą po `CustomExceptions`; łamanie reguły biznesowej →
   `DomainRuleException` (409), złe dane → własny `Invalid*Exception` (400). Identyfikatory: VO `Id` i `UserId`.
6. Daty `DateTime` UTC. Domena **nie woła** `DateTime.UtcNow` – czas dostaje parametrem; w Application czas daje `IClock`.
7. Status zamiast wielu booli (`AnnouncementStatus`, `OfferStatus`).
8. EF: każdą relację konfiguruj **raz** (wewnątrz agregatu – w konfiguracji korzenia). Konwertery VO i enumy (jako string) globalnie
   w `ConfigureConventions`. VO z kilkoma polami → `ComplexProperty`/`OwnsOne`. Między agregatami tylko FK, bez nawigacji.
9. SQL Server: domyślnie `DeleteBehavior.Restrict`, `Cascade` tylko dla części agregatu.
10. Async wszędzie (`CancellationToken`), repozytorium per agregat (interfejs w Core). Repozytoria **nie** wołają `SaveChanges` –
    robi to `IUnitOfWork.ExecuteAsync(...)` (`SqlUnitOfWork` = transakcja + SaveChanges + przeniesienie zdarzeń do outboksa).
11. Nazwy w kodzie po angielsku, komentarze mogą być po polsku.

## Model domeny (Core) – szczegóły metod i reguł: `docs/ARCHITEKTURA.md` „Model domeny”
- `User` (+ `ContactData`), `RefreshToken` (hash, rotacja), `UserDevice` (token FCM, max 10) – `Entities/Users`
- `Announcement` (agregat): Offers, Images (max 5), Revision (token współbieżności, +1 przy realnej edycji), Changes; `AggregateRoot`
  z `DomainEvents`. `Category` → `Subcategory`. `Rating` (raz na ogłoszenie i oceniającego). `LikedAnnouncement`, `LikedUser`.
- `Conversation` (`Entities/Negotiations`): jeden wątek na (ogłoszenie, pomocnik), wiadomości, znaczniki przeczytania.
- `Notification` (`Entities/Notifications`): powstaje w tle z outboksa. `AnnouncementAlert` (`Entities/Alerts`): obszar / trasa.

## Przepływ statusów
Announcement: `Open` → (AcceptOffer) `Accepted` → (Start) `InProgress` → (Complete) `Completed`; `Open`/`Accepted` → `Cancelled`.
Offer: `Pending` → `Accepted` | `Rejected` | `Withdrawn` | `Countered` (zastąpiona kontrofertą).
Akceptuje/odrzuca/kontruje **druga strona** niż autor oferty; wycofuje tylko autor. Oferty tylko w `Open`; po akceptacji pozostałe
`Pending` → `Rejected`. Oferta sprzed ostatniej edycji jest nieaktualna (`IsOutdated`) – nie da się jej przyjąć, tylko kontrować.
`Start` – właściciel albo zaakceptowany pomocnik; `Complete`/`Cancel` – właściciel. Ocena tylko po `Completed`, przez uczestnika.

## Gdzie co leży
`docs/`: `ARCHITEKTURA.md` (szczegóły obszarów), `PLAN-NEGOCJACJE.md` (Kroki 9–12), `PLAN-POWIADOMIENIA.md` (Kroki 13–17),
`PLAN-FRONTEND.md` (F1–F10 + wspólna kolejność), `HISTORIA-KROKOW.md` (pełny stan prac).
```
Core            Entities, ValueObjects, Enums, Events, Exceptions, Repositories (interfejsy + IUnitOfWork)
Application     Abstractions, DTO, Queries, Commands (+ Handlers), Exceptions, Events, Notifications (handlery zdarzeń,
                NotificationPublisher, kanały, NotificationTextFormatter, IPushSender), Alerts, Security, Time, Extensions
Infrastructure  DAL (DbContext, Converters, Configurations, Repositories, Handlers = query handlery, Migrations), SqlUnitOfWork,
                DbInitializer, Security (JWT, hasła), Outbox, Push (FCM / log), Notifications (e-mail – log), Time, Extensions
Api             Program.cs, Controllers, ErrorHandling/DomainExceptionHandler, Notifications (hub SignalR), ICanHelp.Api.http
Api.Tests       Infrastructure/ApiFactory (kontener, RecordingPushSender), Infrastructure/TestApi (helpery), testy per obszar
.github/workflows/ci.yml   backend: build + testy (z Dockerem); frontend: npm ci, lint, test, build
```
- Zapytania (`GetX`) definiuje Application, handlery są w Infrastructure (czytają z DbContext, `AsNoTracking`). Handlery komend –
  Application (`Commands/Handlers`, repozytoria + `IUnitOfWork`). Rejestracja: Scrutor. Wspólne: `RepositoryExtensions`
  (`GetOrThrowAsync`, `GetActiveOrThrowAsync`), `AsUtc()`.
- Testy integracyjne: jeden kontener i baza na przebieg, środowisko `Testing`, config przez `UseSetting`, outbox ręcznie
  (`ProcessOutboxAsync()`). Bazy nie czyścimy – każdy test ma własnych użytkowników i filtruje po swoich danych.

## Api w skrócie (szczegóły: `docs/ARCHITEKTURA.md`)
- Kontrolery: handler wstrzykiwany do akcji (`[FromServices] IQueryHandler<,>` / `ICommandHandler<>`, bez mediatora), `null` z query → 404.
  Id aktora zawsze z tokenu (`User.TryGetUserId`), pola komend ustawiane przez kontroler mają `[JsonIgnore]`.
- Błędy jako ProblemDetails (`DomainExceptionHandler`): *NotFound → 404 (nowy wyjątek dopisz do listy!), `DomainRuleException`,
  `ConcurrencyConflict`, `AlreadyRated`, `*AlreadyInUse` → 409, inne `CustomExceptions` → 400, `InvalidCredentials` → 401,
  `UserLocked` → 403; `errorCode` = nazwa wyjątku bez „Exception”, zawsze `traceId`.
- Auth: JWT HS256 (`Auth:SigningKey` ≥ 32 bajty, poza Development z env) + refresh tokeny z rotacją. Prywatność: oferty widzi właściciel
  (wszystkie) i pomocnik (swoje), kontakt po akceptacji, wątki tylko dla dwóch uczestników (obcy → 404), cudze zasoby `api/me/*` → 404.
- Endpointy: `api/users` (+ refresh, sign-out), `api/categories`, `api/announcements` (+ offers, images, conversations, contact,
  ratings, like), `api/conversations/{id}`, `api/me/` conversations | notifications (+ hub `/hubs/notifications`) | alerts | devices.
  Przykłady w `ICanHelp.Api.http`. Porty 5080/7080, `/health`, Swagger w Development.
- Migracje w `Infrastructure/DAL/Migrations`: `dotnet ef migrations add` działa (Krok 15), ale sprawdź wynik – EF nie zna indeksu
  przestrzennego `IX_Announcements_GeoPoint` (kolumna `GeoPoint`); linie, które go dotykają, usuń.
- Sekrety poza repo (env / user-secrets projektu Api): `Auth:SigningKey`, `Push:Fcm:CredentialsPath` (plik JSON konta serwisowego).

## Komendy
```bash
verify.cmd                                    # wszystko po kroku (Windows) → .verify/report.md; -Quick = bez Dockera
serve-local.cmd                               # build frontu → Api/wwwroot → https://localhost:7080 (PWA, push); -NoBuild
dotnet build
dotnet test                                   # integracyjne wymagają działającego Dockera
dotnet test --filter "Category!=Integration"  # tylko jednostkowe
docker compose up -d                          # SQL Server w Dockerze
dotnet tool restore                           # dotnet-ef z manifestu
dotnet run --project ICanHelp.Api             # http://localhost:5080/swagger; przy starcie migracje + seed
dotnet ef migrations add <Name> -p ICanHelp.Infrastructure -s ICanHelp.Api -o DAL/Migrations
cd ICanHelp.Web && npm ci && npm start        # http://localhost:4200 (proxy /api, /hubs → :5080)
```

## Stan prac (aktualizuj!) – pełna historia: `docs/HISTORIA-KROKOW.md`
- [x] Backend, Kroki 1–15: domena, Api + ProblemDetails, JWT + refresh, ogłoszenia/oferty/oceny, odległość, edycja z rewizjami,
  zdjęcia, wątki negocjacji, powiadomienia (outbox, SignalR), alerty obszar/trasa, push FCM + `api/me/devices`.
  Ostatnia weryfikacja 10.11.2026 (`verify.cmd`): dotnet 468/468 (Core 198, Application 179, integracyjne 91).
- [x] Frontend, F1–F6: szkielet, logowanie, przeglądanie (lista ↔ mapa), dodawanie/edycja, oferty i rozmowy, powiadomienia
  w aplikacji. Ostatnia weryfikacja 10.11.2026: lint + Vitest 182/182 + build zielone.
- [x] F7 – PWA + push (10.11.2026, `verify.cmd`: Vitest 209/209): manifest, ikony, jeden SW `app-sw.js` (FCM + ngsw), instalacja,
  `PushService` → `api/me/devices`. Zostało ręcznie: `firebase` + `vapidKey` w `environment*.ts`, test Android / iPhone przez HTTPS.
- [x] F8 – alerty (12.11.2026, `verify.cmd`: dotnet 468/468, Vitest 250/250): `/profile/alerts` (lista, włącz/wyłącz, usuń),
  obszar / trasa (OpenRouteService), podgląd `/preview`. Zostało ręcznie: `openRouteServiceKey` w `environment*.ts`.
- [ ] **W toku: F10 wdrożenie** = MVP (Augustów / Suwałki). 12.11.2026: API serwuje frontend (`wwwroot`, `serve-local.cmd`),
  PWA + push sprawdzone lokalnie przez HTTPS w Chrome (`verify.cmd`: dotnet 479/479, Vitest 250/250). Android OK (tunel cloudflared). Dalej: iPhone, hosting.
- [ ] F11 – poprawki po teście autora (`docs/PLAN-FRONTEND.md`: A błędy, B nawigacja, C aktualizacja oferty) – przed testem z ludźmi
- [ ] Krok 16 – „jestem w okolicy” na żądanie → F9 (ekran) → Krok 17 (Capacitor, sklepy; opcjonalnie lokalizacja w tle)

## Jak pracować z AI (oszczędnie)
- Jedna sesja = jeden krok. Na start: „Przeczytaj CLAUDE.md i docs/PLAN-…md, zrób Krok N” (frontend: Krok FN).
- Podawaj konkretne pliki, zamiast prosić o przeszukanie repo.
- Po kroku: jedna linia w „Stan prac”, szczegóły w `docs/HISTORIA-KROKOW.md` (i `docs/ARCHITEKTURA.md`, jeśli doszedł obszar).
- Sesja lokalna (Windows): AI samo uruchamia `verify.cmd`, czyta `.verify/report.md` i poprawia aż do „WSZYSTKO OK”.
- Sesja w chmurze (bez .NET/Dockera/npm): autor uruchamia `verify.cmd` i pisze „sprawdź raport”; AI czyta raport (logi:
  `.verify/*.log`), poprawia i wpisuje wynik do „Stan prac” i historii. Commit robi autor.
