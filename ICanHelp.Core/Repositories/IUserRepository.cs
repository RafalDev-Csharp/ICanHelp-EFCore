using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default);

    Task<User?> GetByUserNameAsync(UserName userName, CancellationToken cancellationToken = default);

    Task<bool> ExistsByUserNameAsync(UserName userName, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
