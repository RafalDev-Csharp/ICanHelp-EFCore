using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

internal sealed class GetMyDevicesHandler : IQueryHandler<GetMyDevices, IReadOnlyList<DeviceDto>>
{
    private readonly ICanHelpDbContext _db;

    public GetMyDevicesHandler(ICanHelpDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DeviceDto>> HandleAsync(GetMyDevices query,
        CancellationToken cancellationToken = default)
    {
        var userId = new UserId(query.UserId);

        var devices = await _db.UserDevices
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.LastSeenAt)
            .ToListAsync(cancellationToken);

        return devices.Select(d => d.AsDto()).ToList();
    }
}

internal sealed class GetMyDeviceByTokenHandler : IQueryHandler<GetMyDeviceByToken, DeviceDto?>
{
    private readonly ICanHelpDbContext _db;

    public GetMyDeviceByTokenHandler(ICanHelpDbContext db)
    {
        _db = db;
    }

    public async Task<DeviceDto?> HandleAsync(GetMyDeviceByToken query, CancellationToken cancellationToken = default)
    {
        var userId = new UserId(query.UserId);

        var device = await _db.UserDevices
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.Token == query.Token && d.UserId == userId, cancellationToken);

        return device?.AsDto();
    }
}

internal static class DeviceMapping
{
    public static DeviceDto AsDto(this UserDevice device) => new()
    {
        Id = device.Id,
        Platform = device.Platform,
        CreatedAt = device.CreatedAt,
        LastSeenAt = device.LastSeenAt
    };
}
