using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Repositories;

public interface IUserDeviceRepository
{
    Task<UserDevice?> GetByIdAsync(Id id, CancellationToken cancellationToken = default);

    Task<UserDevice?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>All devices of the user (push to all of them, limit of devices).</summary>
    Task<IReadOnlyList<UserDevice>> GetByUserAsync(UserId userId, CancellationToken cancellationToken = default);

    Task AddAsync(UserDevice device, CancellationToken cancellationToken = default);

    void Remove(UserDevice device);
}
