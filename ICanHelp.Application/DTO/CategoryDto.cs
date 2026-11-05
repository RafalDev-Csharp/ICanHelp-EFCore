namespace ICanHelp.Application.DTO;

public sealed record CategoryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<SubcategoryDto> Subcategories { get; init; } = [];
}
