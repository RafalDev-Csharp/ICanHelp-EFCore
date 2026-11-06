using System.Text.Json.Serialization;
using ICanHelp.Application.Abstractions;

namespace ICanHelp.Application.Commands;

/// <summary>
/// Pomocnik zakłada wątek z właścicielem ogłoszenia (np. żeby zapytać o coś przed złożeniem oferty).
/// Jeden wątek na parę (ogłoszenie, pomocnik): jeśli już istnieje, komenda nic nie robi.
/// Wątek powstaje też automatycznie przy pierwszej ofercie (<see cref="MakeOffer"/>).
/// </summary>
public sealed record StartConversation(Guid AnnouncementId, Guid ConversationId, Guid HelperId) : ICommand;

/// <summary>Wiadomość w wątku (2–500 znaków). Piszą tylko właściciel i pomocnik wątku.</summary>
public sealed record SendMessage : ICommand
{
    [JsonIgnore] public Guid ConversationId { get; init; }
    [JsonIgnore] public Guid MessageId { get; init; }
    [JsonIgnore] public Guid SenderId { get; init; }

    public string Text { get; init; } = string.Empty;
}

/// <summary>Uczestnik przeczytał wątek do teraz (licznik nieprzeczytanych się zeruje).</summary>
public sealed record MarkConversationAsRead(Guid ConversationId, Guid UserId) : ICommand;
