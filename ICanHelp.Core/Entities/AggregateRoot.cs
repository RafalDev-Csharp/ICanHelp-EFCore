using ICanHelp.Core.Events;

namespace ICanHelp.Core.Entities;

/// <summary>
/// Base of aggregates that raise domain events. The events wait here until the unit of work saves them
/// to the outbox (same transaction as the change) and clears them. Not mapped by EF.
/// </summary>
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
