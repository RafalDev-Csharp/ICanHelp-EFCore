using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Repositories;

internal sealed class UserDeviceRepository : IUserDeviceRepository
{
    private readonly ICanHelpDbContext _db;

    public UserDeviceRepository(ICanHelpDbContext db)
    {
        _db = db;
    }

    public Task<UserDevice?> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
        => _db.UserDevices.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<UserDevice?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
        => _db.UserDevices.SingleOrDefaultAsync(d => d.Token == token, cancellationToken);

    public async Task<IReadOnlyList<UserDevice>> GetByUserAsync(UserId userId,
        CancellationToken cancellationToken = default)
        => await _db.UserDevices.Where(d => d.UserId == userId).ToListAsync(cancellationToken);

    public async Task AddAsync(UserDevice device, CancellationToken cancellationToken = default)
        => await _db.UserDevices.AddAsync(device, cancellationToken);

    public void Remove(UserDevice device) => _db.UserDevices.Remove(device);
}
