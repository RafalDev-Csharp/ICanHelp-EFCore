namespace ICanHelp.Infrastructure.Outbox;

/// <summary>
/// Handles waiting outbox messages. Called in a loop by <see cref="OutboxBackgroundService"/>; integration tests call it
/// directly ("process everything that is waiting now"), so they do not have to wait for the interval.
/// </summary>
public interface IOutboxProcessor
{
    /// <summary>Processes waiting messages until none are left; returns how many were taken (also failed ones).</summary>
    Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default);
}
