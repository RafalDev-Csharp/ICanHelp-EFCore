using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Queries;

public sealed class GetCategoryById : IQuery<CategoryDto?>
{
    public Guid CategoryId { get; init; }
}
