using ICanHelp.Core.Events;

namespace ICanHelp.Application.Events;

/// <summary>
/// Reaction to a domain event, called by the outbox processor (Infrastructure) after the change was committed –
/// never inside the command. The same event can be delivered more than once (retry after an error), so handlers
/// must be idempotent; <paramref name="eventId"/> (id of the outbox message) is stable for that.
/// </summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(Guid eventId, TEvent domainEvent, CancellationToken cancellationToken = default);
}
