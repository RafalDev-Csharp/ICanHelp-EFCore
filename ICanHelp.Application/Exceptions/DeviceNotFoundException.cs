using ICanHelp.Core.Exceptions;

namespace ICanHelp.Application.Exceptions;

public class DeviceNotFoundException : CustomExceptions
{
    public Guid DeviceId { get; }

    public DeviceNotFoundException(Guid deviceId)
        : base($"Device with id: {deviceId} was not found.")
    {
        DeviceId = deviceId;
    }
}
