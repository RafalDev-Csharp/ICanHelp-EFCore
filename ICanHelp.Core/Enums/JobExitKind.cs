namespace ICanHelp.Core.Enums;

/// <summary>F11 E: how one side left an accepted job.</summary>
public enum JobExitKind
{
    /// <summary>Before the start (Accepted): the announcement goes back to Open, the accepted offer becomes Abandoned. No ratings.</summary>
    Abandoned = 1,

    /// <summary>During the work (InProgress): the announcement ends as Interrupted. Both sides can rate each other.</summary>
    Interrupted = 2
}
