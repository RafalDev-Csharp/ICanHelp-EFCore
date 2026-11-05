namespace ICanHelp.Core.Repositories;

public interface IUnitOfWork
{
    /// <summary>Runs the action in a transaction and saves all changes made by it.</summary>
    Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default);
}
