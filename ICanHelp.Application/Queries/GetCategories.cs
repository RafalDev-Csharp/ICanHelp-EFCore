using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Queries;

/// <summary>All categories with their subcategories.</summary>
public sealed class GetCategories : IQuery<IReadOnlyList<CategoryDto>>
{
}
