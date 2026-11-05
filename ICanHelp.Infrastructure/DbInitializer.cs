using ICanHelp.Core.Entities;
using ICanHelp.Core.ValueObjects;
using ICanHelp.Infrastructure.DAL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ICanHelp.Infrastructure;

/// <summary>On application start: applies pending migrations and seeds the categories.</summary>
internal sealed class DbInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public DbInitializer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // A hosted service is a singleton, the DbContext is scoped - so we create a scope.
        using var scope = _serviceProvider.CreateScope();
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
            ["Plumbing"] = ["Leaking tap", "Blocked drain", "Toilet repair"],
            ["Painting"] = ["Wall painting", "Small repairs before painting"],
            ["Assembly"] = ["Hanging pictures and shelves", "Furniture assembly"],
            ["Electrical"] = ["Lamp installation", "Socket or switch replacement"],
            ["Advice"] = ["Computer help", "Tutoring", "Other advice"]
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
