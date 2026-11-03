using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Core.Enums;

namespace ICanHelp.Application.Queries;

/// <summary>A page of announcements, newest first. Every filter is optional.</summary>
public sealed class GetAnnouncements : IQuery<IReadOnlyList<AnnouncementDto>>
{
    public Guid? OwnerId { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? SubcategoryId { get; init; }
    public AnnouncementStatus? Status { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
