using System.Collections.Concurrent;
using ICanHelp.Application.Notifications;

namespace ICanHelp.Api.Tests.Infrastructure;

/// <summary>
/// Push w testach integracyjnych (Krok 15): zamiast FCM zapamiętuje, co i na jakie tokeny wysłano. Wspólny dla całego
/// przebiegu – testy szukają swoich unikalnych tokenów. Tokeny z <see cref="InvalidTokens"/> „odrzuca FCM”.
/// </summary>
public sealed class RecordingPushSender : IPushSender
{
    public ConcurrentQueue<(IReadOnlyCollection<string> Tokens, PushMessage Message)> Sent { get; } = new();
    public ConcurrentDictionary<string, bool> InvalidTokens { get; } = new();

    public Task<PushSendResult> SendAsync(IReadOnlyCollection<string> tokens, PushMessage message,
        CancellationToken cancellationToken = default)
    {
        Sent.Enqueue((tokens.ToList(), message));
        return Task.FromResult(new PushSendResult(tokens.Where(InvalidTokens.ContainsKey).ToList()));
    }

    /// <summary>Wiadomości wysłane na dany token.</summary>
    public IReadOnlyList<PushMessage> SentTo(string token)
        => Sent.Where(s => s.Tokens.Contains(token)).Select(s => s.Message).ToList();
}
