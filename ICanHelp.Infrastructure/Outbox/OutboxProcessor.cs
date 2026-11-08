using ICanHelp.Application.Events;
using ICanHelp.Application.Time;
using ICanHelp.Core.Events;
using ICanHelp.Infrastructure.DAL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ICanHelp.Infrastructure.Outbox;

/// <summary>
/// Takes waiting messages (oldest first, in batches), calls every <see cref="IDomainEventHandler{TEvent}"/> of the event
/// and marks the message as processed. Each message in its own DI scope (own DbContext), so one failure does not
/// affect the others. A failure → Attempts++ and Error; the message is retried in the next round until
/// <see cref="OutboxOptions.MaxAttempts"/>. Handlers are idempotent (the message id is the event id), so a message
/// handled twice (e.g. the handler committed, but marking as processed failed) gives no duplicates.
/// <para>
/// One round at a time within the process (semaphore): the background loop and a test calling
/// <see cref="ProcessPendingAsync"/> do not run in parallel. Several API instances would need row locking
/// (<c>UPDLOCK, READPAST</c>) – not needed yet.
/// </para>
/// </summary>
internal sealed class OutboxProcessor : IOutboxProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClock _clock;
    private readonly IOptions<OutboxOptions> _options;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly SemaphoreSlim _round = new(1, 1);

    public OutboxProcessor(IServiceScopeFactory scopeFactory, IClock clock, IOptions<OutboxOptions> options,
        ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _options = options;
        _logger = logger;
    }

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        await _round.WaitAsync(cancellationToken);
        try
        {
            var options = _options.Value;
            var total = 0;
            var tried = new HashSet<Guid>();

            while (true)
            {
                var ids = await GetWaitingAsync(options, tried, cancellationToken);
                if (ids.Count == 0)
                {
                    return total;
                }

                foreach (var id in ids)
                {
                    tried.Add(id);
                    await ProcessAsync(id, cancellationToken);
                }

                total += ids.Count;
            }
        }
        finally
        {
            _round.Release();
        }
    }

    // A message that failed in this round is not taken again in the same round (it waits for the next one).
    private async Task<List<Guid>> GetWaitingAsync(OutboxOptions options, HashSet<Guid> tried,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICanHelpDbContext>();

        var ids = await db.OutboxMessages
            .AsNoTracking()
            .Where(m => m.ProcessedAt == null && m.Attempts < options.MaxAttempts)
            .OrderBy(m => m.OccurredAt)
            .ThenBy(m => m.Id)
            .Select(m => m.Id)
            .Take(options.BatchSize + tried.Count)
            .ToListAsync(cancellationToken);

        return ids.Where(id => !tried.Contains(id)).Take(options.BatchSize).ToList();
    }

    private async Task ProcessAsync(Guid messageId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ICanHelpDbContext>();

            var message = await db.OutboxMessages.SingleOrDefaultAsync(m => m.Id == messageId, cancellationToken);
            if (message is null || message.ProcessedAt is not null)
            {
                return;
            }

            var domainEvent = DomainEventSerializer.Deserialize(message.Type, message.Payload);
            await DispatchAsync(scope.ServiceProvider, message.Id, domainEvent, cancellationToken);

            message.MarkAsProcessed(_clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Outbox message {MessageId} failed.", messageId);
            await RecordFailureAsync(messageId, exception, cancellationToken);
        }
    }

    private static async Task DispatchAsync(IServiceProvider services, Guid eventId, IDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
        var handle = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;

        // No handler (e.g. AnnouncementPublished until Krok 14) = nothing to do, the message is still processed.
        foreach (var handler in services.GetServices(handlerType))
        {
            await (Task)handle.Invoke(handler, [eventId, domainEvent, cancellationToken])!;
        }
    }

    // New scope: the failed one may hold half-added entities (e.g. notifications) that must not be saved.
    private async Task RecordFailureAsync(Guid messageId, Exception exception, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ICanHelpDbContext>();

            var message = await db.OutboxMessages.SingleOrDefaultAsync(m => m.Id == messageId, cancellationToken);
            if (message is null)
            {
                return;
            }

            message.MarkAsFailed($"{exception.GetType().Name}: {exception.Message}");
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception recordException) when (recordException is not OperationCanceledException)
        {
            _logger.LogError(recordException, "Could not record the failure of outbox message {MessageId}.", messageId);
        }
    }
}
