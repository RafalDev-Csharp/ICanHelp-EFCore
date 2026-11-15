namespace ICanHelp.Core.Services;

/// <summary>
/// The beginning of a longer text for places with little room (list of conversations, notifications):
/// at most <see cref="MaxLength"/> characters, cut between words if possible, with "…" at the end.
/// </summary>
public static class TextPreview
{
    public const int MaxLength = 100;

    public static string Of(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length <= MaxLength)
        {
            return trimmed;
        }

        var cut = trimmed[..MaxLength];
        var lastSpace = cut.LastIndexOf(' ');
        // Nie tniemy w połowie słowa, chyba że to jedno bardzo długie „słowo”.
        if (lastSpace > MaxLength / 2)
        {
            cut = cut[..lastSpace];
        }

        return cut.TrimEnd() + "…";
    }
}
