using ICanHelp.Core.Entities;
using ICanHelp.Core.ValueObjects;
using ICanHelp.Infrastructure.DAL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ICanHelp.Infrastructure;

/// <summary>On application start: applies pending migrations and seeds the categories.</summary>
internal sealed class DbInitializer(IServiceProvider serviceProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // A hosted service is a singleton, the DbContext is scoped - so we create a scope.
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICanHelpDbContext>();

        if (!db.Database.GetMigrations().Any())
        {
            return; // no migrations yet (dotnet ef migrations add Init)
        }

        await db.Database.MigrateAsync(cancellationToken);

        if (await db.Categories.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Categories.AddRange(CreateCategories());
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static IEnumerable<Category> CreateCategories()
    {
        var data = new Dictionary<string, string[]>
        {
            // Nazwy kategorii = klucze ikon na froncie (ICanHelp.Web/src/app/core/categories/category-icons.ts).
            ["Hydraulika"] =
                ["Cieknący kran / spłuczka", "Udrażnianie odpływu", "Montaż armatury i AGD", "Inne"],
            ["Elektryka"] =
                ["Gniazdka i włączniki", "Montaż lamp", "Drobne awarie", "Inne"],
            ["Drobne naprawy domowe"] =
                ["Złota rączka", "Drzwi, okna, zamki", "Wieszanie półek, karniszy, obrazów", "Uszczelnianie, silikonowanie", "Inne"],
            ["Malowanie i wykończenia"] =
                ["Malowanie ścian", "Tapetowanie", "Drobne poprawki (gips, fugi)", "Inne"],
            ["Meble i montaż"] =
                ["Składanie mebli", "Naprawa mebli", "Montaż TV i sprzętu na ścianie", "Inne"],
            ["Ogród i podwórko"] =
                ["Koszenie trawy", "Przycinanie drzew i krzewów", "Sprzątanie posesji", "Odśnieżanie", "Inne"],
            ["Sprzątanie"] =
                ["Sprzątanie mieszkania", "Mycie okien", "Sprzątanie po remoncie", "Pranie tapicerki", "Inne"],
            ["Przeprowadzki i transport"] =
                ["Pomoc przy przeprowadzce", "Wniesienie / zniesienie", "Przewóz rzeczy", "Wywóz gratów", "Inne"],
            ["Auto i rower"] =
                ["Wymiana kół i opon", "Drobne naprawy auta", "Rozruch, pomoc na drodze", "Serwis roweru", "Inne"],
            ["Komputery i elektronika"] =
                ["Pomoc z komputerem / telefonem", "Internet, Wi-Fi, drukarka", "Naprawa sprzętu RTV/AGD", "Inne"],
            ["Korepetycje i nauka"] =
                ["Matematyka i przedmioty ścisłe", "Języki obce", "Przygotowanie do egzaminów", "Nauka gry na instrumencie", "Inne"],
            ["Porady i pomoc codzienna"] =
                ["Opieka nad zwierzętami", "Zakupy i sprawy do załatwienia", "Pomoc osobom starszym", "Pisma i formalności", "Inne"]
        };

        foreach (var (categoryName, subcategories) in data)
        {
            var category = new Category(Id.New(), categoryName);

            foreach (var subcategoryName in subcategories)
            {
                category.AddSubcategory(Id.New(), subcategoryName);
            }

            yield return category;
        }
    }
}
