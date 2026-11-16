namespace ICanHelp.Core.Enums;

/// <summary>Days of the week as flags (e.g. "Monday, Tuesday" or <see cref="Workdays"/>) – when an alert is active.</summary>
[Flags]
public enum WeekDays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,

    Workdays = Monday | Tuesday | Wednesday | Thursday | Friday,
    Weekend = Saturday | Sunday,
    All = Workdays | Weekend
}

public static class WeekDaysExtensions
{
    public static bool Includes(this WeekDays days, DayOfWeek day) => (days & From(day)) != 0;

    public static WeekDays From(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => WeekDays.Monday,
        DayOfWeek.Tuesday => WeekDays.Tuesday,
        DayOfWeek.Wednesday => WeekDays.Wednesday,
        DayOfWeek.Thursday => WeekDays.Thursday,
        DayOfWeek.Friday => WeekDays.Friday,
        DayOfWeek.Saturday => WeekDays.Saturday,
        _ => WeekDays.Sunday
    };
}
