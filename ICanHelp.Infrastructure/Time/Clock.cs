using ICanHelp.Application.Time;

namespace ICanHelp.Infrastructure.Time;

internal sealed class Clock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
