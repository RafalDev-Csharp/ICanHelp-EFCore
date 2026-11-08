# Klient API (NSwag)

`api-client.ts` jest **generowany** – nie edytuj go ręcznie.

1. Uruchom backend: `dotnet run --project ICanHelp.Api` (Swagger na http://localhost:5080/swagger).
2. W `ICanHelp.Web`: `npm run api` (konfiguracja: `nswag.json`, wymaga .NET 9).
3. Kompilator pokaże miejsca do poprawy po zmianie API.

Nazwy: operationId `Kontroler_Akcja` z backendu (`Program.cs`, `CustomOperationIds`) → klasa `KontrolerClient`
i metoda `akcja`, np. `POST api/users/sign-in` (`UsersController.Login`) → `UsersClient.login(...)`.
DTO to interfejsy (`typeStyle: Interface`), daty to `string` w UTC – parsuj przez `parseApiDate`.
