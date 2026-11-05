using ICanHelp.Core.Repositories;

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
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
