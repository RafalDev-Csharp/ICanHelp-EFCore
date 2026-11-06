using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Core.Enums;

namespace ICanHelp.Application.Queries;

/// <summary>
/// „Moje negocjacje”: wątki, w których <see cref="UserId"/> jest właścicielem ogłoszenia albo pomocnikiem,
/// od ostatniej aktywności (<see cref="MyConversationDto.LastActivityAt"/>). Filtry są opcjonalne.
/// </summary>
public sealed record GetMyConversations : IQuery<IReadOnlyList<MyConversationDto>>
{
    /// <summary>Kto czyta (z tokenu).</summary>
    public Guid UserId { get; init; }

    /// <summary>Client = tylko moje ogłoszenia, Helper = tylko tam, gdzie pomagam; null = oba.</summary>
    public OfferParty? Role { get; init; }

    /// <summary>Status ogłoszenia (np. tylko Open – trwające negocjacje).</summary>
    public AnnouncementStatus? Status { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
