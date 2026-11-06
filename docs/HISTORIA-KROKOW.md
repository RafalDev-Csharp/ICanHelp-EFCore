# ICanHelpApp – historia kroków

> Pełny „Stan prac” z `CLAUDE.md` (co zrobiono w każdym kroku, testy, migracje, weryfikacje). W `CLAUDE.md` zostaje skrót.

## Stan prac
- [x] Szkielet: Core, Application (DTO, Queries), Infrastructure
- [x] Przebudowa domeny (Core) – model powyżej
- [x] Krok 1 – Infrastructure + Application dopasowane do nowego Core (konfiguracje EF, repozytoria, query handlery)
- [x] Krok 1b – build zielony, usunięty przestarzały `LocationConfiguration.cs`
- [x] Krok 2 – `ICanHelp.Core.Tests` (xUnit + Shouldly): `Entities/Announcement*Tests.cs`, dane w `AnnouncementTestData.cs` (`Now`, `InStatus(...)`).
- [x] Krok 3 – projekt `ICanHelp.Api`: kontrolery GET dla wszystkich zapytań, Swagger, ProblemDetails (`DomainExceptionHandler`),
  appsettings, launchSettings, `/health`. `dotnet build` zielony u autora (3.11.2026).
- [x] Krok 4 – `docker-compose.yml` (SQL Server 2022), manifest `dotnet-ef`, migracja `Init` (20261105141305), seed kategorii.
- [x] Krok 5 – rejestracja/logowanie (JWT, hash hasła): opis w sekcji „Uwierzytelnianie”, projekt `ICanHelp.Application.Tests`
  (14 testów `SignUpHandler`/`SignInHandler`). Bez nowej migracji (schemat Users bez zmian).
- [x] Krok 6 – komendy ogłoszeń/ofert/oceny (`Commands/AnnouncementCommands.cs`, `OfferCommands.cs`) + endpointy + kontakt po akceptacji.
  Testy: `Core.Tests/.../AnnouncementParticipantsTests.cs`, `Application.Tests/Commands/AnnouncementHandlersTests.cs`. Bez migracji.

- [x] Krok 7 – wyszukiwanie po odległości (opis w sekcji „Infrastructure”), migracja `AnnouncementGeoPoint` (20261105190000,
  pisana ręcznie), testy `Application.Tests/Queries/GetAnnouncementsDistanceFilterTests.cs`.

- [x] Krok 8 – `ICanHelp.Api.Tests` (Testcontainers.MsSql 4.4.0, Mvc.Testing 9.0.11; ~30 testów: auth, prywatność, cały przepływ
  negocjacji, wyszukiwanie po odległości, ProblemDetails), CI `.github/workflows/ci.yml`, README z diagramami Mermaid.
  Zweryfikowane u autora 5.11.2026: `dotnet test` – 172/172 zielone (potwierdza też Kroki 5–7). **Zostało: push + zielony build w Actions.**

- [x] Krok 9 – PATCH ogłoszenia + rewizje (stare/nowe wartości pól → podświetlanie zmian) + blokada akceptacji nieaktualnej oferty
  (szczegóły: `docs/PLAN-NEGOCJACJE.md`). Migracja `AnnouncementRevisions` (20261106120000, pisana ręcznie). 6.11.2026: build + 195/195 testów zielone.
  Poprawka (różnica netto „było → jest”, `EditsSinceOffer`) – zweryfikowana w pełnym przebiegu Kroku 10.
- [x] Krok 10 – zdjęcia (sekcja „Zdjęcia”), migracja `ImageAddedAt` (20261106150000, ręczna). Testy: `Core.Tests/.../AnnouncementImageTests.cs`,
  `Application.Tests/Commands/ImageHandlersTests.cs`, `Api.Tests/AnnouncementImageTests.cs`. 6.11.2026: build + 241/241 testów zielone
  (w tym integracyjne z migracją; potwierdza też test `LaterEdit_CannotHideEarlierPriceChange` z Kroku 9).
- [x] Krok 11 – wątek negocjacji + prywatność ofert (sekcja „Rozmowa”), migracja `Conversations` (20261106170000, ręczna, z backfillem
  wątków dla istniejących ofert). Testy: `Core.Tests/.../ConversationTests.cs`, `Application.Tests/Commands/ConversationHandlersTests.cs`,
  `Api.Tests/ConversationTests.cs`. Pisane bez kompilatora; zweryfikowane w przebiegu Kroku 12 (286/286).
- [x] Krok 12 – „moje negocjacje”: `GET api/me/conversations` (sekcja „Rozmowa”), bez migracji. Testy: `Api.Tests/MyConversationsTests.cs`,
  `Application.Tests/Queries/MyConversationPreviewTests.cs`. 6.11.2026: build + 286/286 testów zielone
  (w tym integracyjne z migracją `Conversations`), commit 77f0e73.
- [ ] Krok 13 – powiadomienia (opcjonalnie): `docs/PLAN-NEGOCJACJE.md`
