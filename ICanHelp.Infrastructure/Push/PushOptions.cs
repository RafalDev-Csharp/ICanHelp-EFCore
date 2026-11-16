namespace ICanHelp.Infrastructure.Push;

/// <summary>
/// Section "Push" (Krok 15). The service account key of Firebase is a secret – never in the repo. Locally: a file outside
/// the repo + <c>dotnet user-secrets set "Push:Fcm:CredentialsPath" "C:\...\icanhelp-firebase.json"</c>; on a server:
/// env <c>Push__Fcm__CredentialsPath</c> (mounted file) or <c>Push__Fcm__CredentialsJson</c> (the content).
/// Off, or no key → <see cref="LoggingPushSender"/> (Development, Testing).
/// </summary>
public sealed class PushOptions
{
    public const string SectionName = "Push";

    public bool Enabled { get; set; }

    public FcmOptions Fcm { get; set; } = new();

    public bool UsesFcm => Enabled
                           && (!string.IsNullOrWhiteSpace(Fcm.CredentialsJson)
                               || !string.IsNullOrWhiteSpace(Fcm.CredentialsPath));
}

public sealed class FcmOptions
{
    /// <summary>Content of the service account JSON from the Firebase console (Project settings → Service accounts).</summary>
    public string? CredentialsJson { get; set; }

    /// <summary>
    /// Path to that JSON file (easier than the content on Windows). Used when <see cref="CredentialsJson"/> is empty.
    /// </summary>
    public string? CredentialsPath { get; set; }

    /// <summary>Optional; empty = the project from the key.</summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Optional address of the web app (e.g. https://icanhelp.pl). Given → Web Push gets an absolute link, so a click
    /// on the system bubble opens the right screen even when the app is closed.
    /// </summary>
    public string? WebAppUrl { get; set; }

    /// <summary>The key: <see cref="CredentialsJson"/>, otherwise the content of <see cref="CredentialsPath"/>.</summary>
    public string ReadCredentialsJson()
    {
        if (!string.IsNullOrWhiteSpace(CredentialsJson))
        {
            return CredentialsJson;
        }

        if (string.IsNullOrWhiteSpace(CredentialsPath))
        {
            throw new InvalidOperationException("Push:Fcm needs CredentialsJson or CredentialsPath.");
        }

        if (!File.Exists(CredentialsPath))
        {
            throw new InvalidOperationException(
                $"Push:Fcm:CredentialsPath points to a file that does not exist: {CredentialsPath}");
        }

        return File.ReadAllText(CredentialsPath);
    }
}
