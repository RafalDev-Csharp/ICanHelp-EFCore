using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Time;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

internal sealed class RegisterDeviceHandler : ICommandHandler<RegisterDevice>
{
    private readonly IUserDeviceRepository _devices;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RegisterDeviceHandler(IUserDeviceRepository devices, IUserRepository users, IUnitOfWork unitOfWork,
        IClock clock)
    {
        _devices = devices;
        _users = users;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task HandleAsync(RegisterDevice command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var userId = new UserId(command.UserId);
            var now = _clock.UtcNow;

            await _users.GetActiveOrThrowAsync(command.UserId, cancellationToken);

            var existing = await _devices.GetByTokenAsync(command.Token, cancellationToken);
            if (existing is not null && existing.IsOwnedBy(userId))
            {
                existing.Refresh(userId, command.Platform, now);
                return;
            }

            // New for this user (a new device or one taken over from another user) – make room under the limit.
            foreach (var old in UserDevice.SelectToRemoveBeforeAdding(
                         await _devices.GetByUserAsync(userId, cancellationToken)))
            {
                _devices.Remove(old);
            }

            if (existing is not null)
            {
                existing.Refresh(userId, command.Platform, now);
                return;
            }

            await _devices.AddAsync(new UserDevice(new Id(command.DeviceId), userId, command.Token, command.Platform, now),
                cancellationToken);
        }, cancellationToken);
}

internal sealed class RemoveDeviceHandler : ICommandHandler<RemoveDevice>
{
    private readonly IUserDeviceRepository _devices;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveDeviceHandler(IUserDeviceRepository devices, IUnitOfWork unitOfWork)
    {
        _devices = devices;
        _unitOfWork = unitOfWork;
    }

    public Task HandleAsync(RemoveDevice command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var device = await _devices.GetByIdAsync(new Id(command.DeviceId), cancellationToken);

            // Someone else's device – as if it did not exist (do not reveal that it does).
            if (device is null || !device.IsOwnedBy(new UserId(command.UserId)))
            {
                throw new DeviceNotFoundException(command.DeviceId);
            }

            _devices.Remove(device);
        }, cancellationToken);
}
