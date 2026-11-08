# Plan: powiadomienia (oferty i wiadomości, alerty o nowych ogłoszeniach „w okolicy” i „po drodze”)

> Plan na Kroki 13–15. Każdy krok to osobna sesja: „Przeczytaj CLAUDE.md i docs/PLAN-POWIADOMIENIA.md, zrób Krok N”.
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
8. **Bieżąca lokalizacja (Krok 15) tylko jako opt-in**, przechowujemy wyłącznie ostatnią pozycję z czasem, bez historii.

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
- [ ] `dotnet build` + `dotnet test`, commit – kod z 8.11.2026 pisany bez kompilatora, czeka na weryfikację

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

## Krok 15 (później, z aplikacją mobilną) – „jestem teraz w okolicy”

- [ ] `UserLocation` (UserId, Location, `GeoPoint`, ReportedAt) – tylko ostatnia pozycja, bez historii; zgoda w ustawieniach (opt-in),
      wyłączenie = usunięcie wiersza.
- [ ] `POST api/me/location` (aplikacja mobilna, API „znaczącej zmiany lokalizacji” – oszczędza baterię; ograniczenie częstotliwości).
- [ ] Alert `Kind = CurrentLocation` (promień wokół bieżącej pozycji, tylko gdy pozycja świeża, np. < 30 min).
- [ ] Dopasowanie w obie strony: nowe ogłoszenie → użytkownicy ze świeżą pozycją w promieniu; nowa pozycja → ogłoszenia `Open`
      z ostatnich 24 h w promieniu (o których jeszcze nie powiadomiono).
- [ ] Push na telefon (FCM/APNs) jako kanał `INotificationSender`.
