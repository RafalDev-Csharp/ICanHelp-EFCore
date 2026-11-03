using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

internal sealed class GetCategoryByIdHandler : IQueryHandler<GetCategoryById, CategoryDto?>
{
    private readonly ICanHelpDbContext _db;

    public GetCategoryByIdHandler(ICanHelpDbContext db)
    {
        _db = db;
    }

    public async Task<CategoryDto?> HandleAsync(GetCategoryById query, CancellationToken cancellationToken = default)
    {
        var id = new Id(query.CategoryId);

        var category = await _db.Categories
            .AsNoTracking()
            .Include(c => c.Subcategories)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

        return category?.AsDto();
    }
}
