using ICanHelp.Core.Entities;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Repositories;

public interface ICategoryRepository
{
    /// <summary>Loads the category together with its subcategories.</summary>
    Task<Category?> GetByIdAsync(Id id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Category category, CancellationToken cancellationToken = default);

    void Remove(Category category);
}
