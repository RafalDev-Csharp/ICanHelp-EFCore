using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Repositories;

internal sealed class UserRepository(ICanHelpDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default)
        => db.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByUserNameAsync(UserName userName, CancellationToken cancellationToken = default)
        => db.Users.SingleOrDefaultAsync(u => u.UserName == userName, cancellationToken);

    public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default)
        => db.Users.SingleOrDefaultAsync(u => u.ContactData.Email == email, cancellationToken);

    public Task<bool> ExistsByUserNameAsync(UserName userName, CancellationToken cancellationToken = default)
        => db.Users.AnyAsync(u => u.UserName == userName, cancellationToken);

    public Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default)
        => db.Users.AnyAsync(u => u.ContactData.Email == email, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
        => await db.Users.AddAsync(user, cancellationToken);
}
