namespace ICanHelp.Application.Alerts;

/// <summary>
/// Rules against spam for alerts about new announcements (section "Alerts" in appsettings; Infrastructure binds it).
/// Days, hours and the quiet hours are in <see cref="TimeZone"/> – the app works in Poland, users do not have
/// their own time zone yet.
/// </summary>
public sealed class AlertOptions
{
    public const string SectionName = "Alerts";

    /// <summary>IANA id (works on Windows too, .NET 6+).</summary>
    public string TimeZone { get; set; } = "Europe/Warsaw";

    /// <summary>
    /// Max "new announcement nearby" notifications per user per (local) day. Above it the user gets one digest
    /// ("and N more") that is updated instead of new notifications.
    /// </summary>
    public int DailyLimit { get; set; } = 20;

    /// <summary>
    /// Quiet hours: the notification is saved (it is on the list in the morning), but not delivered live
    /// (SignalR now, push later).
    /// </summary>
    public bool QuietHoursEnabled { get; set; } = true;

    public TimeSpan QuietHoursFrom { get; set; } = new(22, 0, 0);
    public TimeSpan QuietHoursTo { get; set; } = new(7, 0, 0);

    public TimeZoneInfo GetTimeZone() => TimeZoneInfo.FindSystemTimeZoneById(TimeZone);

    public bool IsQuietAt(DateTime localTime)
    {
        if (!QuietHoursEnabled || QuietHoursFrom == QuietHoursTo)
        {
            return false;
        }

        var time = localTime.TimeOfDay;
        return QuietHoursFrom < QuietHoursTo
            ? time >= QuietHoursFrom && time < QuietHoursTo
            : time >= QuietHoursFrom || time < QuietHoursTo;
    }
}
