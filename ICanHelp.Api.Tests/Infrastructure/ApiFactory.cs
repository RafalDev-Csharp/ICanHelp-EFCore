using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace ICanHelp.Api.Tests.Infrastructure;

/// <summary>
/// Całe API w pamięci (<see cref="WebApplicationFactory{TEntryPoint}"/>) na prawdziwym SQL Serverze z Testcontainers.
/// Jeden kontener i jedna baza na cały przebieg testów (<see cref="ApiCollection"/>). Migracje i seed kategorii
/// robi przy starcie zwykły <c>DbInitializer</c> – testujemy dokładnie to, co uruchamia się na produkcji.
/// Testy nie czyszczą bazy: każdy tworzy własnych użytkowników (unikalny e-mail) i filtruje po swoich danych.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SigningKey = "integration-tests-signing-key-ICanHelp-0123456789";

    // Ten sam obraz co w docker-compose.yml.
    private readonly MsSqlContainer _sql = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();

        // Start hosta od razu: DbInitializer (migracje + seed) wykona się raz, przed pierwszym testem.
        _ = Server;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Osobna baza w kontenerze (Testcontainers podaje "master"); MigrateAsync sama ją utworzy.
        var connectionString = new SqlConnectionStringBuilder(_sql.GetConnectionString())
        {
            InitialCatalog = "ICanHelpTests"
        }.ConnectionString;

        // "Testing": bez Swaggera i bez dev-klucza z appsettings.Development.json – klucz podajemy sami.
        builder.UseEnvironment("Testing");

        // UseSetting (a nie ConfigureAppConfiguration): Program.cs czyta connection string od razu przy rejestracji
        // usług, a ustawienia z UseSetting są widoczne już w builder.Configuration.
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        builder.UseSetting("Auth:SigningKey", SigningKey);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();      // zatrzymuje hosta
        await _sql.DisposeAsync(); // usuwa kontener
    }
}

/// <summary>Wszystkie klasy z <c>[Collection(ApiCollection.Name)]</c> dzielą jedną instancję <see cref="ApiFactory"/>.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
