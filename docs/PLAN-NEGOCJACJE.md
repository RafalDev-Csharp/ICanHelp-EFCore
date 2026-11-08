# Plan: edycja ogłoszenia, rozmowa w negocjacji, lista „moje negocjacje”

> Plan na Kroki 9–12 (Kroki 13–15 – powiadomienia: `docs/PLAN-POWIADOMIENIA.md`). Każdy krok to osobna sesja: „Przeczytaj CLAUDE.md i docs/PLAN-NEGOCJACJE.md, zrób Krok N”.
> Po kroku: odhacz punkty tutaj, zaktualizuj „Stan prac” w CLAUDE.md, `dotnet build` + `dotnet test`, commit.

## Dlaczego tak (decyzje, których nie zmieniamy bez powodu)

1. **Oferty są niezmienne.** Zmiana warunków = kontroferta (nowy `Offer` z `ParentOfferId`). Zostaje historia i nie da się
   zaakceptować czegoś, co druga strona w tej samej chwili zmieniła. Dlatego **nie ma PATCH dla ofert**.
2. **Ogłoszenie można edytować (PATCH)** – tylko właściciel, tylko w `Open`. Każda edycja, która coś zmienia,
   podbija `Announcement.Revision` (0 = wersja opublikowana) i zapisuje wpis `AnnouncementChange` (rewizja, czas, które pola).
3. **Każda oferta pamięta rewizję ogłoszenia** (`Offer.AnnouncementRevision`), na której ją złożono. Odczyt liczy z tego
   `IsOutdated` + `ChangesSinceOffer` – per pole wartość **„było” (co widział autor oferty) → „jest”** → frontend pokazuje
   „⚠ ogłoszenie zmieniło się po tej ofercie” i **podświetla różnicę** (tytuł/opis: diff słów, np. biblioteka `diff` / jsdiff;
   reszta: stara wartość przekreślona, nowa pogrubiona). Każda edycja zapisuje stare i nowe wartości (`AnnouncementFieldChange`).
4. **Nieaktualnej oferty nie da się zaakceptować** (`DomainRuleException` → 409), nawet po zmianie jednej litery – jedna litera
   potrafi zmienić sens zlecenia. Chroni obie strony przed przyjęciem warunków do innej treści. Wyjście: kontroferta
   (powstaje na bieżącej rewizji). Decyzja potwierdzona przez autora 6.11.2026.
5. **`Revision` to token współbieżności** (EF `IsConcurrencyToken`). Edycja i akceptacja w tej samej chwili → jedna z nich
   dostaje 409 (`ConcurrencyConflict`) zamiast cichego nadpisania.
6. **Powiadomienia:** backend daje *stan* (rewizje, `IsOutdated`, nieprzeczytane). Wysyłanie (push/e-mail/SignalR) to osobny,
   opcjonalny Krok 13 – frontend do tego czasu odpytuje (polling) listę negocjacji.
7. **Rozmowa ≠ oferta.** Pytania i wyjaśnienia („dlaczego drożej?”) to wiadomości w wątku. Oferta to formalna propozycja,
   którą da się zaakceptować. Wątek jest osobnym agregatem (`Conversation`), żeby czat nie ładował i nie blokował ogłoszenia.

## Krok 9 – edycja ogłoszenia + rewizje + ostrzeżenie w ofertach

- [x] Core: enum `AnnouncementFields` ([Flags]: Category, Difficulty, Title, Description, Budget, Location, Images)
- [x] Core: encja `AnnouncementChange` (część agregatu; klucz `AnnouncementId + Revision`, `ChangedAt`, `ChangedFields`,
      `Fields` = `AnnouncementFieldChange` (Field, OldValue, NewValue) – osobna tabela `AnnouncementFieldChanges`)
- [x] Core: `Announcement.Revision`, `UpdatedAt`, `Changes`; `Update(actorId, ..., changedAtUtc)` – tylko właściciel, zwraca zmienione
      pola, bez zmian = bez nowej rewizji; `GetChangesSince(revision)`
- [x] Core: `Offer.AnnouncementRevision`; `AcceptOffer` odrzuca ofertę złożoną na starszej rewizji
- [x] Application: komenda `UpdateAnnouncement` (pola opcjonalne – `null` = bez zmian) + `UpdateAnnouncementHandler`
- [x] Application: `ConcurrencyConflictException` (→ 409)
- [x] DTO: `AnnouncementDto.Revision/UpdatedAt`, `AnnouncementDetailsDto.Changes[].Fields[]`, `OfferDto.AnnouncementRevision/IsOutdated/ChangesSinceOffer[]`
      (`AnnouncementFieldChangeDto { field, oldValue, newValue }`)
- [x] Infrastructure: konfiguracja EF (`AnnouncementChangeConfiguration`, `Revision` jako concurrency token, konwersja flag na string),
      `Include(Changes)` w repozytorium i w `GetAnnouncementByIdHandler`, `SqlUnitOfWork` zamienia `DbUpdateConcurrencyException`
- [x] Api: `PATCH api/announcements/{id}` (204), mapowanie `ConcurrencyConflictException` → 409
- [x] Testy: Core (`AnnouncementRevisionTests`), Application (`UpdateAnnouncementHandlerTests`), Api (`AnnouncementEditTests`)
- [x] Migracja `20261106120000_AnnouncementRevisions` napisana ręcznie (+ snapshot). Gdyby EF zgłosił różnice modelu: `dotnet ef migrations add AnnouncementRevisions -p ICanHelp.Infrastructure -s ICanHelp.Api -o DAL/Migrations`
      (sprawdź: nowe kolumny `Announcements.Revision`, `Announcements.UpdatedAt`, `Offers.AnnouncementRevision`, tabele
      `AnnouncementChanges` i `AnnouncementFieldChanges`; domyślne 0 jest poprawne – istniejące dane to rewizja 0). **Uwaga:** EF nie zna indeksu
      przestrzennego – jeśli migracja próbuje coś robić z `GeoPoint`/`IX_Announcements_GeoPoint`, usuń te linie.
- [x] Poprawka po review: `Announcement.GetFieldChangesSince(rev)` (Core, różnica netto per pole – późniejsza edycja nie ukryje
      wcześniejszej zmiany ceny; pola cofnięte do identycznej treści pomijane), `GetEditsSince(rev)` → `OfferDto.EditsSinceOffer`
      (np. „⚠ edytowano 2 razy po Twojej ofercie”). Testy Core + nowy test Api `LaterEdit_CannotHideEarlierPriceChange`
      (**Api jeszcze nieuruchomiony** – przy następnym pełnym przebiegu z Dockerem)
- [x] Build (7/7) + testy 195/195 zielone (Visual Studio, 6.11.2026), w tym 35 integracyjnych z migracją
- [x] Commit (5a491b6)

## Krok 10 – zdjęcia ogłoszenia (upload / usuwanie)

- [x] Core: `AddImage(actorId, imageId, ..., addedAtUtc)` / `RemoveImage(actorId, imageId, changedAtUtc)` – tylko właściciel, tylko `Open`,
      każda zmiana podbija rewizję z flagą `Images` (`RecordChange`). Stare/nowe wartości = id zdjęć w kolejności dodania
      („id1, id2”) – nie nazwy plików, bo dwa różne zdjęcia mogą mieć tę samą nazwę. `Image.AddedAt`, `GetThumbnail()`,
      nieznane zdjęcie → `ImageNotFoundException` (404, wcześniej `DomainRule`)
- [x] Application: `IFileStorage` – **zmiana względem planu:** `SaveAsync(path, stream)`, ścieżkę wybiera aplikacja
      (`ImageFiles.GetStoragePath`), żeby domena sprawdziła reguły *przed* zapisem pliku; `OpenReadAsync`, `DeleteAsync`.
      Komendy `AddAnnouncementImage`, `RemoveAnnouncementImage`; walidacja: format po „magic bytes” (jpg/png/webp), max 5 MB
      (liczone przy czytaniu, nie z nagłówka), max 5 zdjęć (domena). Błąd po zapisie pliku → plik kasowany.
- [x] Infrastructure: `LocalFileStorage` (`Storage:ImagesPath`, ochrona przed `../`), `GET api/announcements/{id}/images/{imageId}`
      (query `GetAnnouncementImage`), migracja `20261106150000_ImageAddedAt`
- [x] Api: `POST api/announcements/{id}/images` (multipart/form-data, pole `file`, 201), `DELETE api/announcements/{id}/images/{imageId}` (204)
- [x] Miniatura: najstarsze zdjęcie (`AnnouncementDto.ThumbnailUrl`, na liście jako podzapytanie); mniejsze pliki – później
- [x] Testy Core (`AnnouncementImageTests`), Application (`ImageHandlersTests`), Api (`AnnouncementImageTests`: upload, pobranie,
      limit, zły plik, za duży, nie-właściciel → 409, oferta `IsOutdated` po dodaniu zdjęcia, usuwanie)
- [x] `dotnet build` + `dotnet test` – 241/241 zielone (6.11.2026), commit

## Krok 11 – wątek negocjacji (wiadomości) + prywatność ofert

- [x] Core: agregat `Conversation` (Id, AnnouncementId, ClientId = właściciel, HelperId, CreatedAt, `ClientLastReadAt`, `HelperLastReadAt`,
      `Messages`); część agregatu `ConversationMessage` (Id, SenderId, Text (Description, max 500), SentAt, `AnnouncementRevision`).
      Jeden wątek na parę (AnnouncementId, HelperId) – unikalny indeks.
- [x] Reguły: wątek zakłada pomocnik (może pytać **przed** złożeniem oferty) albo powstaje automatycznie przy `MakeOffer`;
      pisać mogą tylko te dwie osoby; pisanie dozwolone w `Open`, a po akceptacji tylko w wątku zaakceptowanego pomocnika
      (status ogłoszenia handler czyta z repozytorium ogłoszeń); `MarkAsRead(userId, atUtc)`.
      **Realizacja:** reguła „czy wolno pisać” jest w domenie – `Announcement.AllowsConversationWith(helperId)`
      (po akceptacji: Accepted/InProgress – tylko zaakceptowany pomocnik; Completed/Cancelled – nikt, wątek tylko do odczytu;
      decyzja autora 6.11.2026: po zakończeniu zostają już tylko wzajemne oceny), `Conversation.SendMessage`
      dostaje ogłoszenie parametrem. Wysłanie wiadomości = nadawca przeczytał wątek do teraz.
- [x] Api: `POST api/announcements/{id}/conversations` (pomocnik zakłada wątek, 201 albo 200 z istniejącym id),
      `GET api/conversations/{id}` (wątek: wiadomości + oferty tego pomocnika + zmiany ogłoszenia jako jedna oś czasu),
      `POST api/conversations/{id}/messages`, `POST api/conversations/{id}/read`
- [x] Oś czasu w odczycie: wiadomości, oferty (`HelperId` = pomocnik wątku) i wpisy „ogłoszenie zmienione (opis, zdjęcia)”
      z `AnnouncementChanges` – posortowane po czasie; to są te „systemowe” znaki ostrzegawcze w rozmowie.
      **Realizacja:** `ConversationDto.Timeline[]` = `{ type: Message|Offer|AnnouncementChanged, at, message?, offer?, change? }`;
      zmiany tylko po `CreatedAt` wątku; dodatkowo `myRole`, `unreadCount`, `canWrite`
- [x] **Prywatność ofert:** `GET api/announcements/{id}` dla obcych i niezalogowanych – bez ofert (tylko liczba);
      właściciel widzi wszystkie; pomocnik – tylko swoje. (Dziś każdy widzi ceny i wiadomości wszystkich pomocników.)
      Popraw istniejące testy Api, które czytają `Offers` jako właściciel/pomocnik – powinny dalej przechodzić.
      **Realizacja:** `Announcement.GetOffersVisibleTo(viewer)`, liczba jako `AnnouncementDetailsDto.HelperCount` (ilu pomocników);
      istniejące testy czytają oferty jako właściciel albo autor – bez zmian.
- [x] Migracja `20261106170000_Conversations` napisana ręcznie (+ snapshot, Designer); zakłada wątki dla istniejących par (ogłoszenie, pomocnik) z ofert
- [x] Testy Core (`ConversationTests`), Application (`ConversationHandlersTests`), Api (`ConversationTests`, w tym prywatność ofert)
- [x] `dotnet build` + `dotnet test` – zweryfikowane razem z Krokiem 12 (286/286, 6.11.2026), commit

## Krok 12 – „Moje negocjacje” (lista rozmów z podglądem ogłoszenia)

- [x] Query `GetMyConversations` (handler w Infrastructure, `AsNoTracking`, stronicowanie):
      `GET api/me/conversations` → `[{ conversationId, announcementId, title, thumbnailUrl, announcementStatus, myRole (Client/Helper),
      otherParty { id, userName }, lastOffer { id, price, status, proposedBy, isOutdated, createdAt }, lastMessagePreview, lastActivityAt,
      unreadCount, announcementChangedSinceLastRead }]`, sortowanie po `lastActivityAt` malejąco
      **Realizacja:** jedno zapytanie SQL ze skalarnymi podzapytaniami (ostatnia wiadomość/oferta/zmiana, liczniki nieprzeczytanych
      dla obu stron); `lastActivityAt` = max(założenie wątku, ostatnia wiadomość, ostatnia oferta, ostatnia edycja ogłoszenia),
      sortowanie + stronicowanie w pamięci (wątków jednego użytkownika jest niewiele; gdy urośnie → zdenormalizowana kolumna
      `Conversations.LastActivityAt`). `announcementChangedSinceLastRead` – tylko dla pomocnika (właściciel edytował sam);
      nigdy nieczytany wątek = zmiany po jego założeniu. Podgląd wiadomości: 100 znaków, cięcie na granicy słowa + „…”.
      Endpoint w `ConversationsController` (`~/api/me/conversations`). Akceptacja/odrzucenie oferty nie zmienia `lastActivityAt`
      (oferta nie ma daty zmiany statusu) – zmiana statusu widać w `lastOffer.status`.
- [x] Filtry: `role=Client|Helper`, `status=Open|...` (+ `page`, `pageSize` 1–100)
- [x] Indeksy pod zapytanie – już z Kroku 11 (Conversations po ClientId / HelperId, Messages po ConversationId + SentAt,
      Offers po AnnouncementId + HelperId + Status, AnnouncementChanges po kluczu AnnouncementId + Revision). Bez migracji.
- [x] Testy Api (`MyConversationsTests`: dwie rozmowy naraz i kolejność po aktywności, kontroferta, stronicowanie, licznik
      nieprzeczytanych z obu stron, flaga zmiany ogłoszenia + `isOutdated`, filtry rola/status, 401) + Application
      (`MyConversationPreviewTests`)
- [x] `dotnet build` + `dotnet test` – 286/286 zielone (6.11.2026), commit 77f0e73

## Krok 13 – powiadomienia

Przeniesione i rozszerzone (alerty „w okolicy” / „po drodze”): `docs/PLAN-POWIADOMIENIA.md`.
