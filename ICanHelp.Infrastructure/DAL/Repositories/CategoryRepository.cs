using ICanHelp.Core.Entities;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Repositories;

internal sealed class CategoryRepository : ICategoryRepository
{
    private readonly ICanHelpDbContext _db;

    public CategoryRepository(ICanHelpDbContext db)
    {
        _db = db;
    }

    public Task<Category?> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
        => _db.Categories
            .Include(c => c.Subcategories)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _db.Categories
            .Include(c => c.Subcategories)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
        => await _db.Categories.AddAsync(category, cancellationToken);

    public void Remove(Category category) => _db.Categories.Remove(category);
}
