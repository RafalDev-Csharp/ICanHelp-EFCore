using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Repositories;

internal sealed class UserRepository : IUserRepository
{
    private readonly ICanHelpDbContext _db;

    public UserRepository(ICanHelpDbContext db)
    {
        _db = db;
    }

    public Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default)
        => _db.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByUserNameAsync(UserName userName, CancellationToken cancellationToken = default)
        => _db.Users.SingleOrDefaultAsync(u => u.UserName == userName, cancellationToken);

    public Task<bool> ExistsByUserNameAsync(UserName userName, CancellationToken cancellationToken = default)
        => _db.Users.AnyAsync(u => u.UserName == userName, cancellationToken);

    public Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default)
        => _db.Users.AnyAsync(u => u.ContactData.Email == email, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
        => await _db.Users.AddAsync(user, cancellationToken);
}
