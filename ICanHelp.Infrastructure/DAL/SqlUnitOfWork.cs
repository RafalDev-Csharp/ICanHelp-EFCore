using ICanHelp.Application.Exceptions;
using ICanHelp.Core.Entities;
using ICanHelp.Core.Repositories;
using ICanHelp.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL;

/// <summary>
/// Transaction + SaveChanges. Before saving, domain events of the tracked aggregates go to the outbox
/// (<see cref="OutboxMessage"/>) – in the same transaction, so a change is never saved without its event
/// and an event never exists for a change that was rolled back. The events are sent later by the outbox processor.
/// </summary>
internal sealed class SqlUnitOfWork(ICanHelpDbContext dbContext) : IUnitOfWork
{
    public async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await action();
            MoveDomainEventsToOutbox();
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone changed the same aggregate in the meantime (concurrency token, e.g. Announcement.Revision).
            await transaction.RollbackAsync(cancellationToken);
            throw new ConcurrencyConflictException();
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private void MoveDomainEventsToOutbox()
    {
        var aggregates = dbContext.ChangeTracker
            .Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            dbContext.OutboxMessages.AddRange(aggregate.DomainEvents.Select(DomainEventSerializer.ToOutboxMessage));
            aggregate.ClearDomainEvents();
        }
    }
}
