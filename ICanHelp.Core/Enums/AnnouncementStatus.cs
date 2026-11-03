namespace ICanHelp.Core.Enums;

public enum AnnouncementStatus
{
    /// <summary>Visible to helpers, accepts offers.</summary>
    Open = 1,

    /// <summary>One offer was accepted, work has not started yet.</summary>
    Accepted = 2,

    InProgress = 3,
    Completed = 4,
    Cancelled = 5
}
