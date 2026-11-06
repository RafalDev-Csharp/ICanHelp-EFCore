using ICanHelp.Application.Exceptions;
using ICanHelp.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL;

internal sealed class SqlUnitOfWork : IUnitOfWork
{
    private readonly ICanHelpDbContext _dbContext;

    public SqlUnitOfWork(ICanHelpDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await action();
            await _dbContext.SaveChangesAsync(cancellationToken);
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
}
