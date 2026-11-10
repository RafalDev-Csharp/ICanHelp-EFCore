using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using ICanHelp.Application.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ICanHelp.Infrastructure.Push;

/// <summary>
/// Push through Firebase Cloud Messaging (HTTP v1, NuGet <c>FirebaseAdmin</c>) – one sender for Web (PWA), Android
/// and iOS. One multicast per notification (a user has at most 10 devices, FCM takes up to 500 tokens).
/// <c>UNREGISTERED</c> / <c>SENDER_ID_MISMATCH</c> / <c>INVALID_ARGUMENT</c> for a token → it comes back as invalid and
/// the device is removed; but when every token fails with <c>INVALID_ARGUMENT</c>, the message itself is wrong –
/// then nothing is removed (only logged).
/// </summary>
internal sealed class FcmPushSender : IPushSender
{
    private const string AppName = "icanhelp-push";

    private readonly FirebaseMessaging _messaging;
    private readonly FcmOptions _options;
    private readonly ILogger<FcmPushSender> _logger;

    public FcmPushSender(IOptions<PushOptions> options, ILogger<FcmPushSender> logger)
    {
        _options = options.Value.Fcm;
        _logger = logger;

        var app = FirebaseApp.GetInstance(AppName) ?? FirebaseApp.Create(new FirebaseAdmin.AppOptions
        {
            Credential = CredentialFactory.FromJson<ServiceAccountCredential>(_options.CredentialsJson!)
                .ToGoogleCredential(),
            ProjectId = string.IsNullOrWhiteSpace(_options.ProjectId) ? null : _options.ProjectId
        }, AppName);

        _messaging = FirebaseMessaging.GetMessaging(app);
    }

    public async Task<PushSendResult> SendAsync(IReadOnlyCollection<string> tokens, PushMessage message,
        CancellationToken cancellationToken = default)
    {
        if (tokens.Count == 0)
        {
            return PushSendResult.None;
        }

        var tokenList = tokens.ToList();
        var response = await _messaging.SendEachForMulticastAsync(ToMulticast(tokenList, message), cancellationToken);
        if (response.FailureCount == 0)
        {
            return PushSendResult.None;
        }

        var invalid = new List<string>();
        var invalidArgument = 0;

        for (var i = 0; i < response.Responses.Count; i++)
        {
            if (response.Responses[i].Exception is not { } exception)
            {
                continue;
            }

            if (exception.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.SenderIdMismatch)
            {
                invalid.Add(tokenList[i]);
            }
            else if (exception.ErrorCode == ErrorCode.InvalidArgument)
            {
                invalidArgument++;
                invalid.Add(tokenList[i]);
            }
            else
            {
                _logger.LogWarning(exception, "Push to one device failed ({Code}).", exception.MessagingErrorCode);
            }
        }

        if (invalidArgument == tokenList.Count && tokenList.Count > 1)
        {
            _logger.LogWarning("Push rejected for every device (INVALID_ARGUMENT) – the message is wrong, no device removed.");
            return PushSendResult.None;
        }

        return new PushSendResult(invalid);
    }

    private MulticastMessage ToMulticast(IReadOnlyList<string> tokens, PushMessage message)
    {
        // FirebaseAdmin 3.7 marks Tokens obsolete in favour of installation ids (Fids), but the web SDK (getToken)
        // and Capacitor still give registration tokens – and FCM HTTP v1 takes them.
#pragma warning disable CS0618
        var multicast = new MulticastMessage
        {
            Tokens = tokens,
            Notification = new Notification { Title = message.Title, Body = message.Body },
            Data = message.Data
        };
#pragma warning restore CS0618

        if (!string.IsNullOrWhiteSpace(_options.WebAppUrl) && message.Data.TryGetValue("url", out var url))
        {
            multicast.Webpush = new WebpushConfig
            {
                FcmOptions = new WebpushFcmOptions { Link = _options.WebAppUrl.TrimEnd('/') + url }
            };
        }

        return multicast;
    }
}
