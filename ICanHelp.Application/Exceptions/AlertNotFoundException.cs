using ICanHelp.Core.Exceptions;

namespace ICanHelp.Application.Exceptions;

/// <summary>The alert does not exist or belongs to somebody else (we do not reveal which) → 404.</summary>
public class AlertNotFoundException : CustomExceptions
{
    public Guid AlertId { get; }

    public AlertNotFoundException(Guid alertId)
        : base($"Alert with id: {alertId} was not found.")
    {
        AlertId = alertId;
    }
}
