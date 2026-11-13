namespace ICanHelp.Core.Events;

// F11 D: realizacja zlecenia (po przyjęciu oferty). Każde zdarzenie zna obie strony – handler wybiera odbiorcę bez wczytywania.

/// <summary>The accepted helper started the work (Accepted → InProgress).</summary>
public sealed record JobStarted(
    Guid AnnouncementId,
    Guid OwnerId,
    Guid HelperId,
    DateTime OccurredAt) : IDomainEvent;

/// <summary>The accepted helper reported the work as done – the owner should confirm it.</summary>
public sealed record JobDoneReported(
    Guid AnnouncementId,
    Guid OwnerId,
    Guid HelperId,
    DateTime OccurredAt) : IDomainEvent;

/// <summary>The owner confirmed the work (→ Completed) – both sides can rate each other.</summary>
public sealed record JobCompleted(
    Guid AnnouncementId,
    Guid OwnerId,
    Guid HelperId,
    DateTime OccurredAt) : IDomainEvent;
