using System.Text.Json;

namespace ICanHelp.Application.Notifications;

/// <summary>Parameters of a notification stored as a JSON object of strings (<c>Notification.Payload</c>).</summary>
public static class NotificationPayload
{
    public const string AnnouncementTitle = "announcementTitle";
    public const string Price = "price";
    public const string ProposedBy = "proposedBy";
    public const string Reason = "reason";
    public const string MessagePreview = "messagePreview";
    public const string Revision = "revision";
    public const string ChangedFields = "changedFields";

    /// <summary>Value of <see cref="Reason"/> when an offer was rejected because the owner chose another helper.</summary>
    public const string AnotherOfferAccepted = "AnotherOfferAccepted";

    public static string Serialize(IReadOnlyDictionary<string, string> parameters)
        => JsonSerializer.Serialize(parameters);

    public static IReadOnlyDictionary<string, string> Deserialize(string payload)
        => JsonSerializer.Deserialize<Dictionary<string, string>>(payload) ?? new Dictionary<string, string>();
}
