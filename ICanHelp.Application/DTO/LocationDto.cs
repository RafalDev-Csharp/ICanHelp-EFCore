namespace ICanHelp.Application.DTO;

public sealed record LocationDto
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}
