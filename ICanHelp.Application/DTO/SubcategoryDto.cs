namespace ICanHelp.Application.DTO;

public sealed record SubcategoryDto
{
    public Guid Id { get; init; }
    public Guid CategoryId { get; init; }
    public string Name { get; init; } = string.Empty;
}
