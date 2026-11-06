using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Queries;

public sealed class GetAnnouncementById : IQuery<AnnouncementDetailsDto?>
{
    public Guid AnnouncementId { get; init; }

    /// <summary>
    /// Kto czyta (z tokenu; null = niezalogowany). Oferty widzi tylko właściciel (wszystkie) i pomocnik (swoje) –
    /// reszta dostaje tylko liczbę pomocników (<see cref="AnnouncementDetailsDto.HelperCount"/>).
    /// </summary>
    public Guid? ViewerId { get; init; }
}
