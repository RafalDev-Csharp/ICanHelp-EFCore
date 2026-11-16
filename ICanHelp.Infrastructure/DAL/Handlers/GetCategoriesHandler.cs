using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

internal sealed class GetCategoriesHandler(ICanHelpDbContext db)
    : IQueryHandler<GetCategories, IReadOnlyList<CategoryDto>>
{
    public async Task<IReadOnlyList<CategoryDto>> HandleAsync(GetCategories query,
        CancellationToken cancellationToken = default)
    {
        var categories = await db.Categories
            .AsNoTracking()
            .Include(c => c.Subcategories)
            .ToListAsync(cancellationToken);

        return categories
            .OrderBy(c => c.CategoryName.Value)
            .Select(c => c.AsDto())
            .ToList();
    }
}
