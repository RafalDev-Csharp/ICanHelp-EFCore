using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Queries;

/// <summary>
/// Kontakt do drugiej strony ogłoszenia dla <see cref="UserId"/> (właściciel → pomocnik, pomocnik → właściciel).
/// Tylko w statusach Accepted/InProgress/Completed i tylko dla uczestników; w innym przypadku null (→ 404).
/// </summary>
public sealed class GetAnnouncementContact : IQuery<AnnouncementContactDto?>
{
    public Guid AnnouncementId { get; init; }
    public Guid UserId { get; init; }
}
