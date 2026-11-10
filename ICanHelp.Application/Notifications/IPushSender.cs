namespace ICanHelp.Application.Notifications;

/// <summary>
/// A push message (Krok 15): system title and body + <see cref="Data"/> for the app (notificationId, type,
/// announcementId, conversationId, url – the deep link that a click opens).
/// </summary>
public sealed record PushMessage(string Title, string Body, IReadOnlyDictionary<string, string> Data);

/// <summary>Result of one push: the tokens that FCM no longer accepts (the app was removed, the token expired).</summary>
public sealed record PushSendResult(IReadOnlyCollection<string> InvalidTokens)
{
    public static PushSendResult None { get; } = new(Array.Empty<string>());
}

/// <summary>
/// Sends a push to a list of device tokens (Infrastructure: FCM HTTP v1; without the key – logging only).
/// Invalid tokens come back in the result – <see cref="PushNotificationChannel"/> removes those devices.
/// </summary>
public interface IPushSender
{
    Task<PushSendResult> SendAsync(IReadOnlyCollection<string> tokens, PushMessage message,
        CancellationToken cancellationToken = default);
}
