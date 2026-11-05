namespace ICanHelp.Application.DTO;

public sealed record PriceDto
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
}
