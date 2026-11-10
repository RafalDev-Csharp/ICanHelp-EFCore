namespace ICanHelp.Infrastructure.Push;

/// <summary>
/// Section "Push" (Krok 15). The service account key of Firebase is a secret – never in the repo:
/// env <c>Push__Fcm__CredentialsJson</c> or <c>dotnet user-secrets set "Push:Fcm:CredentialsJson" "..."</c>.
/// Off, or no key → <see cref="LoggingPushSender"/> (Development, Testing).
/// </summary>
public sealed class PushOptions
{
    public const string SectionName = "Push";

    public bool Enabled { get; set; }

    public FcmOptions Fcm { get; set; } = new();

    public bool UsesFcm => Enabled && !string.IsNullOrWhiteSpace(Fcm.CredentialsJson);
}

public sealed class FcmOptions
{
    /// <summary>Content of the service account JSON from the Firebase console (Project settings → Service accounts).</summary>
    public string? CredentialsJson { get; set; }

    /// <summary>Optional; empty = the project from the key.</summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Optional address of the web app (e.g. https://icanhelp.pl). Given → Web Push gets an absolute link, so a click
    /// on the system bubble opens the right screen even when the app is closed.
    /// </summary>
    public string? WebAppUrl { get; set; }
}
