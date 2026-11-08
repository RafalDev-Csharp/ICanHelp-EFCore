namespace ICanHelp.Core.Events;

/// <summary>
/// Something that happened in the domain (an offer was made, a message was sent...). Raised by aggregates
/// (<see cref="Entities.AggregateRoot"/>), saved to the outbox in the same transaction as the change and handled later
/// (notifications). Events carry only plain values (Guid, enums, text), so they can be stored as JSON.
/// </summary>
public interface IDomainEvent
{
    /// <summary>When it happened (UTC) – the time passed to the domain method, never <c>DateTime.UtcNow</c>.</summary>
    DateTime OccurredAt { get; }
}
