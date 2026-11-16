using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Time;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

internal sealed class RegisterDeviceHandler(IUserDeviceRepository devices, IUserRepository users,
    IUnitOfWork unitOfWork, IClock clock) : ICommandHandler<RegisterDevice>
{
    public Task HandleAsync(RegisterDevice command, CancellationToken cancellationToken = default)
        => unitOfWork.ExecuteAsync(async () =>
        {
            var userId = new UserId(command.UserId);
            var now = clock.UtcNow;

            await users.GetActiveOrThrowAsync(command.UserId, cancellationToken);

            var existing = await devices.GetByTokenAsync(command.Token, cancellationToken);
            if (existing is not null && existing.IsOwnedBy(userId))
            {
                existing.Refresh(userId, command.Platform, now);
                return;
            }

            // New for this user (a new device or one taken over from another user) – make room under the limit.
            foreach (var old in UserDevice.SelectToRemoveBeforeAdding(
                         await devices.GetByUserAsync(userId, cancellationToken)))
            {
                devices.Remove(old);
            }

            if (existing is not null)
            {
                existing.Refresh(userId, command.Platform, now);
                return;
            }

            await devices.AddAsync(new UserDevice(new Id(command.DeviceId), userId, command.Token, command.Platform, now),
                cancellationToken);
        }, cancellationToken);
}

internal sealed class RemoveDeviceHandler(IUserDeviceRepository devices, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveDevice>
{
    public Task HandleAsync(RemoveDevice command, CancellationToken cancellationToken = default)
        => unitOfWork.ExecuteAsync(async () =>
        {
            var device = await devices.GetOwnedOrThrowAsync(command.DeviceId, new UserId(command.UserId),
                cancellationToken);
            devices.Remove(device);
        }, cancellationToken);
}
