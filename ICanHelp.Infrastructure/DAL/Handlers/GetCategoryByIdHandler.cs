using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

internal sealed class GetCategoryByIdHandler(ICanHelpDbContext db) : IQueryHandler<GetCategoryById, CategoryDto?>
{
    public async Task<CategoryDto?> HandleAsync(GetCategoryById query, CancellationToken cancellationToken = default)
    {
        var id = new Id(query.CategoryId);

        var category = await db.Categories
            .AsNoTracking()
            .Include(c => c.Subcategories)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

        return category?.AsDto();
    }
}
