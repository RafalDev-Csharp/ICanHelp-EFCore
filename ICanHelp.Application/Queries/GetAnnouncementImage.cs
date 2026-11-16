using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Queries;

/// <summary>The file of one image of an announcement (public, like the announcement). Null = no such image/file.</summary>
public sealed record GetAnnouncementImage : IQuery<ImageFileDto?>
{
    public Guid AnnouncementId { get; init; }
    public Guid ImageId { get; init; }
}
