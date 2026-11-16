namespace ICanHelp.Application.DTO;

public sealed record ImageDto
{
    public Guid Id { get; init; }
    public Guid AnnouncementId { get; init; }

    /// <summary>Original file name (only for display).</summary>
    public string FileName { get; init; } = string.Empty;
    public string FileExtension { get; init; } = string.Empty;

    /// <summary>Where to download the image from, e.g. "/api/announcements/{id}/images/{imageId}".</summary>
    public string Url { get; init; } = string.Empty;
    public DateTime AddedAt { get; init; }

    /// <summary>Relative URL of an image – the same for <see cref="Url"/> and <c>AnnouncementDto.ThumbnailUrl</c>.</summary>
    public static string GetUrl(Guid announcementId, Guid imageId) => $"/api/announcements/{announcementId}/images/{imageId}";
}

/// <summary>An image file to send to the client (GET api/announcements/{id}/images/{imageId}).</summary>
public sealed record ImageFileDto(Stream Content, string ContentType, string FileName);
