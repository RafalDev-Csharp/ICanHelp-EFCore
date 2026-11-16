namespace ICanHelp.Infrastructure.Outbox;

/// <summary>Section "Outbox" in appsettings.</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>Run the background loop. Tests turn it off and call <see cref="IOutboxProcessor"/> themselves.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Pause between rounds when there is nothing (more) to do.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(2);

    public int BatchSize { get; set; } = 50;

    /// <summary>After so many failures a message is not retried any more (Error says why).</summary>
    public int MaxAttempts { get; set; } = 5;
}
