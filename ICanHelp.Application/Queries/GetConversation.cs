using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Queries;

/// <summary>
/// Wątek z osią czasu (wiadomości + oferty tego pomocnika + zmiany ogłoszenia) widziany przez <see cref="UserId"/>.
/// Null, gdy wątku nie ma albo użytkownik nie jest jego uczestnikiem (→ 404, nie zdradzamy, że istnieje).
/// </summary>
public sealed record GetConversation : IQuery<ConversationDto?>
{
    public Guid ConversationId { get; init; }
    public Guid UserId { get; init; }
}

/// <summary>Id wątku pomocnika <see cref="HelperId"/> w ogłoszeniu (null = jeszcze go nie ma).</summary>
public sealed record GetConversationId : IQuery<Guid?>
{
    public Guid AnnouncementId { get; init; }
    public Guid HelperId { get; init; }
}
