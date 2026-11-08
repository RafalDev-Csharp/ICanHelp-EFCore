# Plan: powiadomienia (oferty i wiadomości, alerty o nowych ogłoszeniach „w okolicy” i „po drodze”)

> Plan na Kroki 13–17. Każdy krok to osobna sesja: „Przeczytaj CLAUDE.md i docs/PLAN-POWIADOMIENIA.md, zrób Krok N”.
> Po kroku: odhacz punkty tutaj, zaktualizuj „Stan prac” w CLAUDE.md, `dotnet build` + `dotnet test`, commit.
> Kontekst negocjacji (oferty, wątki, rewizje): `docs/PLAN-NEGOCJACJE.md`.

## Dlaczego tak (decyzje, których nie zmieniamy bez powodu)

1. **Najpierw powiadomienia o transakcji, potem alerty o ogłoszeniach.** Bez powiadomień o ofercie/wiadomości marketplace
   stoi (pomocnik czeka dwa dni na odpowiedź). Krok 13 buduje przy okazji całą „rurę” (zdarzenia → outbox → `Notifications`
   → kanały), z której korzystają Kroki 14–15.
2. **Zdarzenia domenowe + outbox w tej samej transakcji.** Zdarzenie zapisuje się razem ze zmianą (`SqlUnitOfWork`),
   wysyła je osobny `BackgroundService`. Nie ma sytuacji „oferta zapisana, powiadomienie zgubione” ani „powiadomienie
   wysłane, a transakcja wycofana”. Handlery komend nic nie wysyłają same.
3. **Powiadomienie to dane, kanał to sposób dostarczenia.** Najpierw wiersz w `Notifications` (lista w aplikacji, licznik
   nieprzeczytanych), potem kanały: SignalR (użytkownik jest w aplikacji), e-mail / push (nie ma go) – e-mail jako zbiorczy
   skrót, nie po każdej wiadomości.
4. **Trasa „po drodze” nie pochodzi z ciągłego śledzenia GPS.** Użytkownik raz zaznacza punkty (dom, praca), linię drogi
   daje serwis routingu (OSRM / OpenRouteService / Mapbox), zapisujemy ją jako `LINESTRING`. Śledzenie w tle to bateria,
   RODO i dużo kodu, a ręczna trasa daje większość wartości. Uczenie się trasy z GPS – nie planujemy.
5. **Dopasowanie przez gotową strefę + indeks przestrzenny.** Alert trzyma `Zone = Shape.STBuffer(promień)` i szukamy
   `Zone.STIntersects(@punktOgłoszenia) = 1` (spatial index). Nie `Shape.STDistance(@p) <= Radius` – przy promieniu różnym
   w każdym wierszu indeksu nie da się dobrze użyć.
6. **Core nie zna NetTopologySuite** (jak przy `GeoPoint`): domena ma `Location` i listę punktów trasy, `geography`
   istnieje tylko w bazie / Infrastructure (`DAL/Spatial.cs`).
7. **Nie spamujemy:** brak powiadomień o własnych ogłoszeniach i własnych akcjach, jedno powiadomienie na parę
   (ogłoszenie, użytkownik) nawet przy kilku pasujących alertach, dzienny limit (powyżej – skrót), cisza nocna.
8. **Bieżąca lokalizacja (Krok 16) tylko na żądanie**: użytkownik sam włącza dostępność na określony czas, przechowujemy
   wyłącznie ostatnią pozycję z czasem wygaśnięcia, bez historii. Śledzenie w tle – najwcześniej w Kroku 17 (aplikacja ze sklepu).
9. **Klient: najpierw PWA (Angular + Ionic), potem Capacitor; push przez FCM** – szczegóły niżej, „Klient i kanał push”.

## Krok 13 – powiadomienia o ofertach, wiadomościach i edycji (infrastruktura powiadomień)

- [x] Core: `IDomainEvent` (+ `OccurredAt`), baza agregatu z `_domainEvents`, `DomainEvents`, `ClearDomainEvents()` (bez zależności).
      Zdarzenia (`Core/Events`): `OfferMade`, `OfferCountered`, `OfferAccepted`, `OfferRejected`, `OfferWithdrawn`,
      `MessageSent`, `AnnouncementEdited` (rewizja + zmienione pola), `AnnouncementPublished` (pod Krok 14).
      Podnoszą je istniejące metody (`AddOffer`, `CounterOffer`, `AcceptOffer`, …, `Conversation.SendMessage`, `Update`,
      `AddImage/RemoveImage`). Czas zdarzenia = czas przekazany parametrem (domena nie woła `UtcNow`).
- [x] Infrastructure: tabela `OutboxMessages` (Id, Type, Payload JSON, OccurredAt, ProcessedAt?, Attempts, Error?).
      `SqlUnitOfWork` przed `SaveChanges` zbiera zdarzenia z śledzonych agregatów (`ChangeTracker`), zapisuje je do outboksa
      i czyści – ta sama transakcja.
- [x] Infrastructure: `OutboxProcessor : BackgroundService` – co kilka sekund bierze nieprzetworzone (paczka, `ORDER BY OccurredAt`),
      woła handlery zdarzeń z Application, oznacza `ProcessedAt`; błąd → `Attempts++`, `Error`, ponowienie później (limit prób).
      Przetwarzanie idempotentne (to samo zdarzenie dwa razy = jedno powiadomienie).
- [x] Core/Application: encja `Notification` (Id, UserId, Type, Payload/parametry do tekstu, `AnnouncementId?`, `ConversationId?`,
      CreatedAt, ReadAt?, `SourceEventId` – unikalny z `UserId`, dla idempotencji). Odbiorca = druga strona
      (np. `OfferMade` → właściciel, `OfferCountered` → druga strona niż autor, `AnnouncementEdited` → pomocnicy z aktywnymi
      wątkami/ofertami, `MessageSent` → druga osoba w wątku). Nigdy autor akcji.
- [x] Application: `INotificationSender` (kanały); w tym kroku SignalR (`NotificationsHub`, grupa = UserId z JWT).
      E-mail – interfejs `IEmailSender` + implementacja logująca (prawdziwy SMTP później); zbiorczy skrót – opcjonalnie.
- [x] Api: `GET api/me/notifications` (stronicowanie, `unreadOnly`), `GET api/me/notifications/unread-count`,
      `POST api/me/notifications/{id}/read`, `POST api/me/notifications/read-all`; hub `/hubs/notifications`
      (JWT z query `access_token` dla WebSocketów).
- [x] Migracja `Notifications` (OutboxMessages + Notifications, indeksy: Outbox po `ProcessedAt, OccurredAt`;
      Notifications po `UserId, ReadAt, CreatedAt`) – ręcznie, bez linii o `GeoPoint`.
- [ ] Testy: Core (agregaty podnoszą właściwe zdarzenia, w tym brak zdarzenia przy edycji bez zmian), Application
      (wybór odbiorcy, brak powiadomienia dla autora), Api (oferta → właściciel ma powiadomienie; kontroferta → pomocnik;
      licznik + oznaczanie przeczytanych; obcy nie widzi cudzych). W testach integracyjnych outbox przetwarzany
      synchronicznie (metoda testowa / krótki interwał), żeby nie czekać.
- [x] `dotnet build` + `dotnet test`, commit – 8.11.2026: 320/320 zielone

Zrobione w Kroku 13 (odstępstwa od planu): `AcceptOffer`/`RejectOffer`/`WithdrawOffer` dostały parametr czasu (dotąd go nie miały).
Automatyczne odrzucenie pozostałych ofert przy akceptacji też podnosi `OfferRejected` (`AnotherOfferAccepted = true`) – pomocnik
dowiaduje się, że wybrano kogoś innego. `AnnouncementEdited` trafia do pomocników z wątkami (każda oferta zakłada wątek, więc to
obejmuje też tych z ofertami). Zbiorczy skrót e-mail – nie zrobiony (tylko `IEmailSender` + implementacja logująca).
Szczegóły: `docs/ARCHITEKTURA.md`, sekcja „Powiadomienia”.

## Krok 14 – alerty o nowych ogłoszeniach: obszar i trasa

Przykład: mieszkam w Augustowie, pracuję w Suwałkach jako hydraulik, wracam do domu. Chcę wiedzieć o ogłoszeniach
z Hydrauliki do 5 km od domu albo do 5 km od drogi Suwałki → Augustów.

- [x] Core: agregat `AnnouncementAlert` (UserId, Name, `CategoryId`, `SubcategoryIds` (puste = cała kategoria), Kind: `Area` | `Route`,
      `Center` (Location, dla Area), `RoutePoints` (lista Location, dla Route, min. 2), `RadiusKm` (np. 0,5–50),
      `ActiveDays`/`ActiveFrom`/`ActiveTo` (opcjonalnie), IsEnabled, CreatedAt). Limit alertów na użytkownika (np. 10).
      `Matches(categoryId, subcategoryId)` w domenie; geometria w bazie.
- [x] Trasa: frontend wysyła punkty (start, cel) albo gotową polilinię. Wariant 1 (prosty): frontend pobiera trasę z serwisu
      routingu i wysyła polilinię. Wariant 2: backend `IRoutingService` (OSRM/ORS). Zacznij od wariantu 1; polilinię upraszczamy
      (`Reduce` w SQL Server / NTS `DouglasPeuckerSimplifier`), limit liczby punktów.
- [x] Infrastructure: kolumny `Shape geography` (punkt albo LINESTRING) i `Zone geography` (bufor) + indeks przestrzenny na `Zone`
      (w migracji ręcznie, jak `IX_Announcements_GeoPoint`). **Bufor w metrach:** NTS buforuje w stopniach, więc `Zone` liczy
      SQL Server: `UPDATE AnnouncementAlerts SET Zone = Shape.STBuffer(RadiusKm * 1000) WHERE Id = @id` w tej samej transakcji
      (kolumna obliczana z metodą geography nie może być PERSISTED → nie miałaby indeksu). Sprawdź przy implementacji.
- [x] Application: handler zdarzenia `AnnouncementPublished` (z outboksa) → query dopasowania:
      `WHERE IsEnabled AND CategoryId = @c AND (podkategoria pasuje) AND Zone.STIntersects(@point) = 1 AND UserId <> @owner`
      + filtr dni/godzin → `Notification` typu `NewAnnouncementNearby` z odległością i nazwą alertu
      („Nowe ogłoszenie w kategorii Hydraulika: *Cieknący kran* – 3 km od Twojej trasy Suwałki → Augustów”;
      odległość = `Shape.STDistance(@point)`).
- [x] Reguły antyspamowe: jedno powiadomienie na (ogłoszenie, użytkownik), dzienny limit alertów na użytkownika
      (powyżej – jeden skrót „i jeszcze N nowych”), cisza nocna (kolejkujemy na rano albo pomijamy push, zostaje wpis na liście).
- [x] Api: CRUD `api/me/alerts` (GET lista, POST, PATCH – zmiana promienia/kategorii/trasy przelicza `Zone`, DELETE,
      POST `{id}/enable|disable`); opcjonalnie `GET api/me/alerts/{id}/preview` – ogłoszenia `Open`, które pasują teraz.
- [x] Migracja `AnnouncementAlerts` (+ indeks przestrzenny ręcznie).
- [x] Testy: Core (walidacja, limity), Api (punkt w promieniu → powiadomienie, poza → brak; trasa: punkt 3 km od linii →
      jest, 10 km → brak; zła kategoria → brak; własne ogłoszenie → brak; dwa pasujące alerty → jedno powiadomienie).
      Współrzędne testowe: Augustów ~ (53.84, 22.98), Suwałki ~ (54.10, 22.93).
- [x] `dotnet build` + `dotnet test`, commit – 8.11.2026: 371/371 zielone (po poprawce aliasu `Location` w Infrastructure)

Zrobione w Kroku 14 (odstępstwa od planu / decyzje):
- `Zone` liczy **trigger** `TR_AnnouncementAlerts_Zone` (AFTER INSERT, UPDATE; tylko gdy zmienia się `Shape` albo `RadiusKm`):
  `Shape.MakeValid().Reduce(25).STBuffer(RadiusKm * 1000)`. Zamiast osobnego `UPDATE` po `SaveChanges` – działa przy każdym zapisie,
  w tej samej transakcji, a EF wie o nim (`HasTrigger`, bez `OUTPUT`). `Zone` jest w modelu (do zapytań), ale EF go nie zapisuje
  (`PropertySaveBehavior.Ignore`). `Shape` ustawia `ICanHelpDbContext` przed zapisem z `Points` (`Spatial.CreateShape`).
- Upraszczanie trasy: `Reduce(25)` w SQL (tolerancja 25 m przy min. promieniu 500 m) zamiast NTS; limit 1000 punktów w domenie.
  Wariant 2 (`IRoutingService` w backendzie) – nie robiony.
- Punkty i podkategorie alertu – kolumny JSON (`Points` `[[lat,lon],…]`, `SubcategoryIds`). Podkategorie i harmonogram sprawdza
  domena po zapytaniu przestrzennym (`IAnnouncementAlertRepository.FindCandidatesAsync` → `Matches` + `IsActiveAt`).
- Harmonogram: `ActiveDays` (`WeekDays` [Flags]) + opcjonalne godziny (też przez północ). Strefa czasowa jedna dla aplikacji
  (`Alerts:TimeZone` = Europe/Warsaw) – użytkownik nie ma jeszcze własnej.
- Antyspam (`AlertOptions`): jedno powiadomienie na (ogłoszenie, użytkownik) – najbliższy z jego alertów; dzienny limit
  (`DailyLimit` = 20, doba lokalna), powyżej jeden `NewAnnouncementsDigest` na dzień z `count`, aktualizowany (`Notification.Renew` –
  znów nieprzeczytany, na górze listy); cisza nocna 22–7 – wpis zapisany, bez dostarczenia na żywo („kolejkowanie na rano” – nie).
  Ponowienie tego samego zdarzenia może podbić licznik skrótu drugi raz (rzadkie, świadomie pominięte).
- Ogłoszenie już nie `Open`, gdy outbox doszedł (np. anulowane) → bez powiadomień.

## Klient i kanał push – decyzja z 8.11.2026 (dotyczy Kroków 15–17)

- **Najpierw PWA, potem sklep.** Pierwszy frontend to aplikacja webowa „instalowana” na ekranie głównym (PWA): działa na telefonie
  i komputerze, bez sklepów i recenzji, z powiadomieniami push. Później ten sam kod owijamy **Capacitorem** → aplikacja Android/iOS
  w sklepach. Stack: **Angular + Ionic** (komponenty o wyglądzie aplikacji mobilnej) + `@angular/pwa` (service worker).
  Angular, bo przypomina .NET: TypeScript, wstrzykiwanie zależności, serwisy, jedna „oficjalna” struktura projektu.
  Plan kroków frontendu i wspólna kolejność z Krokami 15–17: `docs/PLAN-FRONTEND.md`.
- **Push przez Firebase Cloud Messaging (FCM)** – jeden kanał dla przeglądarki (Web Push), Androida i iOS. Backend się nie zmieni,
  gdy dojdzie aplikacja ze sklepu (inny `Platform` tokenu, ten sam wysyłacz).
- **Czego przeglądarka nie umie:** lokalizacji w tle (pozycja tylko przy otwartej aplikacji). Na iPhonie push w PWA tylko po dodaniu
  do ekranu głównego (iOS 16.4+). Huawei bez usług Google – FCM nie działa (nisza, pomijamy na start).
- **„Jestem w okolicy” na żądanie zamiast śledzenia.** Użytkownik sam włącza dostępność („jestem tu przez 2 h”, „wracam teraz
  trasą Suwałki → Augustów”); pozycję bierzemy raz, przy otwartej aplikacji. Działa w PWA, bez zgody na lokalizację w tle,
  bez problemów z RODO i recenzją w sklepach, a sygnał „mam teraz czas” jest cenniejszy niż samo „przejeżdżam”.
  Automatyczne śledzenie w tle – dopiero w aplikacji ze sklepu, jako opcja (Krok 17).

## Krok 15 – kanał push (FCM) i urządzenia użytkownika

- [ ] Core: agregat `UserDevice` (Id, UserId, Token (FCM, unikalny), Platform: `Web` | `Android` | `Ios`, CreatedAt, LastSeenAt).
      Ten sam token zgłoszony ponownie → odświeżenie `LastSeenAt` (i właściciela, jeśli na urządzeniu zalogował się ktoś inny).
      Limit urządzeń na użytkownika (np. 10, najstarsze wypada).
- [ ] Application: `IPushSender` (wysyłka na listę tokenów, wynik: które tokeny są nieważne). `NotificationTextFormatter` – tytuł
      i treść po polsku z `Type` + `Payload` (np. „Nowa oferta: 150 zł – *Cieknący kran*”). Te same teksty przyda się frontend
      (albo backend zwraca je w `NotificationDto`).
- [ ] Application: `CompositeNotificationSender` – woła wszystkie kanały (SignalR + push); błąd jednego nie blokuje drugiego
      (dziś w `Program.cs` jest jeden `INotificationSender` – zamienić na listę kanałów). Push wysyłamy zawsze; gdy aplikacja jest
      otwarta, frontend nie pokazuje systemowego dymka (FCM w aktywnej karcie trafia do `onMessage`) – bez podwójnych powiadomień.
      Cisza nocna jak dziś (alerty w ciszy nie idą na żywo, więc nie idą też pushem); powiadomienia o transakcji – zawsze.
- [ ] Infrastructure: `FcmPushSender` (NuGet `FirebaseAdmin`, HTTP v1). Klucz konta serwisowego **poza repo** (env / user-secrets,
      `Push:Fcm:CredentialsJson`), `Push:Enabled` – w Testing i bez klucza: implementacja logująca. Odpowiedź `UNREGISTERED` /
      `INVALID_ARGUMENT` → usunięcie `UserDevice`. W wiadomości `data`: `notificationId`, `announcementId?`, `conversationId?`,
      `url` (głęboki link, np. `/announcements/{id}`) – kliknięcie otwiera właściwy ekran.
- [ ] Api: `POST api/me/devices` (token + platforma, zwraca id; idempotentne), `DELETE api/me/devices/{id}` (wylogowanie /
      wyłączenie powiadomień), `GET api/me/devices`. Cudze urządzenie → 404.
- [ ] Migracja `UserDevices` (unikalny indeks na `Token`, indeks na `UserId`).
- [ ] Testy: Core (rejestracja, ponowne zgłoszenie, limit), Application (formatter dla każdego `NotificationType`, composite –
      awaria kanału, nieważny token usuwa urządzenie), Api (rejestracja/usuwanie, obcy → 404, oferta → fake `IPushSender`
      dostał wysyłkę do właściciela, nie do autora).
- [ ] Ręcznie (po starcie frontendu): projekt w konsoli Firebase, klucz VAPID dla Web, test na Androidzie i iPhonie (PWA na ekranie głównym).
- [ ] `dotnet build` + `dotnet test`, commit.

## Krok 16 – „Jestem w okolicy” (dostępność na żądanie)

Przykład: hydraulik kończy pracę w Suwałkach o 16:00, klika „Wracam teraz trasą do domu (2 h)” albo, będąc w Augustowie,
„Jestem tu przez 3 h, 5 km, Hydraulika”. Od razu widzi otwarte ogłoszenia, które pasują, a przez ten czas dostaje push o nowych.

- [ ] Core: alert z czasem wygaśnięcia zamiast nowego mechanizmu – w `AnnouncementAlert`:
      - nowy `Kind = Presence` (punkt = bieżąca pozycja), pole `ExpiresAt?`; najwyżej **jeden** na użytkownika (ponowne włączenie
        nadpisuje pozycję/promień/kategorie/czas), nie wlicza się do limitu 10; czas trwania 0,5–8 h; harmonogram dni/godzin nie dotyczy;
      - dla `Route`: `ActiveUntil?` – „aktywuj teraz na N godzin” (pomija harmonogram dni/godzin, ale nie ciszę nocną);
      - `IsActiveAt(at)` uwzględnia `ExpiresAt`/`ActiveUntil` (czas parametrem, jak wszędzie).
      Ponowne użycie: trigger `Zone`, indeks przestrzenny, `FindCandidatesAsync`, antyspam i `NewAnnouncementNearby` z Kroku 14 –
      bez nowych zapytań dopasowania. Przypadek „kategorie”: wybrane przy włączeniu (domyślnie jak ostatnio); puste = wszystkie.
- [ ] Application/Infrastructure: zapytanie `FindCandidatesAsync` pomija wygasłe (`ExpiresAt > @now`, także w SQL – mniej wierszy).
      Sprzątanie: wygasłe `Presence` usuwane w tle (np. w `OutboxBackgroundService` albo osobnym serwisie, co 15 min) –
      **nie trzymamy historii pozycji**.
- [ ] Dopasowanie w drugą stronę: odpowiedź na włączenie dostępności = lista ogłoszeń `Open` z ostatnich 24 h w strefie
      (jak `preview` z Kroku 14, posortowana po odległości) – pokazana na ekranie, bez powiadomień.
- [ ] Odświeżanie pozycji, póki aplikacja jest otwarta: `PUT api/me/presence` z nową pozycją – nie częściej niż co 1 min i tylko
      przy przesunięciu > 200 m (sprawdza backend, nadmiarowe żądania → 204 bez zapisu). Bez śledzenia w tle.
- [ ] Api: `POST api/me/presence` (lat/lon, promień, kategoria/podkategorie, czas trwania) → lista ogłoszeń w okolicy,
      `PUT api/me/presence` (nowa pozycja), `DELETE api/me/presence` (koniec dostępności = usunięcie wiersza),
      `GET api/me/presence`; `POST api/me/alerts/{id}/activate-now` (body: liczba godzin) dla trasy.
- [ ] Dla zleceniodawcy (opcjonalnie, buduje atrakcyjność): `GET api/announcements/{id}/helpers-nearby` – **tylko liczba**
      pomocników z aktywną dostępnością/trasą pasującą do ogłoszenia, tylko dla właściciela. Nigdy pozycji ani nazwisk.
- [ ] Prywatność: dokładna pozycja nie wychodzi poza użytkownika; zgoda wyrażana kliknięciem przy każdym włączeniu
      (informacja w polityce prywatności – lokalizacja to dane osobowe).
- [ ] Migracja (kolumny `ExpiresAt`, `ActiveUntil`, ewentualnie filtr w indeksach) – ręcznie, bez linii o `GeoPoint`.
- [ ] Testy: Core (jeden `Presence` na użytkownika, limity czasu, `IsActiveAt` z wygaśnięciem, `ActiveUntil` pomija harmonogram),
      Api (włączenie → lista ogłoszeń w promieniu; nowe ogłoszenie w promieniu → powiadomienie; po wygaśnięciu → brak;
      `DELETE` → brak; trasa aktywowana poza harmonogramem → powiadomienie; licznik pomocników tylko dla właściciela).
- [ ] `dotnet build` + `dotnet test`, commit.

## Krok 17 (później, z aplikacją w sklepach) – tryb automatyczny

- [ ] Capacitor: aplikacja Android/iOS z kodu PWA; natywny push (FCM, `Platform = Android/Ios` w `UserDevice` – backend bez zmian).
- [ ] Opcjonalnie lokalizacja w tle (API „znaczącej zmiany lokalizacji” – oszczędza baterię) zasilająca `PUT api/me/presence`
      tylko w oknie włączonej dostępności albo w godzinach alertu trasy. Wymaga zgody „zawsze” i uzasadnienia przy recenzji
      (Google Play: formularz + wideo, Apple: opis użycia) – robimy dopiero, gdy będą użytkownicy i dane, że to potrzebne.
