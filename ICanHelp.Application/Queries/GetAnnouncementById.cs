using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Queries;

public sealed class GetAnnouncementById : IQuery<AnnouncementDetailsDto?>
{
    public Guid AnnouncementId { get; init; }
}
