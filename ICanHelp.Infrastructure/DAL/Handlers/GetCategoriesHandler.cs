using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

internal sealed class GetCategoriesHandler : IQueryHandler<GetCategories, IReadOnlyList<CategoryDto>>
{
    private readonly ICanHelpDbContext _db;

    public GetCategoriesHandler(ICanHelpDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CategoryDto>> HandleAsync(GetCategories query,
        CancellationToken cancellationToken = default)
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .Include(c => c.Subcategories)
            .ToListAsync(cancellationToken);

        return categories
            .OrderBy(c => c.CategoryName.Value)
            .Select(c => c.AsDto())
            .ToList();
    }
}
