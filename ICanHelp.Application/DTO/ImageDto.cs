namespace ICanHelp.Application.DTO;

public sealed record ImageDto
{
    public Guid Id { get; init; }
    public Guid AnnouncementId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string FileExtension { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
}
