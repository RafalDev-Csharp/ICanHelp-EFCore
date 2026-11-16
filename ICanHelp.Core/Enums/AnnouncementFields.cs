namespace ICanHelp.Core.Enums;

/// <summary>Parts of an announcement that can change after publishing. Flags: one edit can change several parts.</summary>
[Flags]
public enum AnnouncementFields
{
    None = 0,
    Category = 1,
    Difficulty = 2,
    Title = 4,
    Description = 8,
    Budget = 16,
    Location = 32,
    Images = 64
}
