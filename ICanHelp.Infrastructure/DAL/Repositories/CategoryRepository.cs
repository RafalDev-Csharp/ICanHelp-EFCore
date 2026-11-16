using ICanHelp.Core.Entities;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Repositories;

internal sealed class CategoryRepository(ICanHelpDbContext db) : ICategoryRepository
{
    public Task<Category?> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
        => db.Categories
            .Include(c => c.Subcategories)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
        => await db.Categories
            .Include(c => c.Subcategories)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
        => await db.Categories.AddAsync(category, cancellationToken);

    public void Remove(Category category) => db.Categories.Remove(category);
}
