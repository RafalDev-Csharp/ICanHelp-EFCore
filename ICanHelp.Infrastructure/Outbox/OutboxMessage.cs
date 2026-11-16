namespace ICanHelp.Infrastructure.Outbox;

/// <summary>
/// A domain event waiting to be handled (transactional outbox). Saved by <c>SqlUnitOfWork</c> in the same transaction
/// as the change that raised it, handled later by <see cref="OutboxProcessor"/>. Not a domain entity.
/// </summary>
public sealed class OutboxMessage
{
    public const int MaxErrorLength = 2000;

    public Guid Id { get; private set; }

    /// <summary>Name of the event class, e.g. "OfferMade" (<see cref="DomainEventSerializer"/>).</summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>The event as JSON.</summary>
    public string Payload { get; private set; } = string.Empty;

    public DateTime OccurredAt { get; private set; }

    /// <summary>When it was handled (null = still waiting).</summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>Failed attempts so far; after <see cref="OutboxOptions.MaxAttempts"/> the message is left alone.</summary>
    public int Attempts { get; private set; }

    /// <summary>The last error (for diagnostics).</summary>
    public string? Error { get; private set; }

    private OutboxMessage()
    {
    }

    public OutboxMessage(Guid id, string type, string payload, DateTime occurredAt)
    {
        Id = id;
        Type = type;
        Payload = payload;
        OccurredAt = occurredAt;
    }

    public void MarkAsProcessed(DateTime processedAtUtc)
    {
        ProcessedAt = processedAtUtc;
        Error = null;
    }

    public void MarkAsFailed(string error)
    {
        Attempts++;
        Error = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
    }
}
