using System.Globalization;
using System.Text;

namespace ICanHelp.Application.Notifications;

/// <summary>
/// Numbers written the Polish way, the same as <c>Intl.NumberFormat("pl-PL")</c> in the web app: decimal comma,
/// thousands separated by a non-breaking space from 10 000 ("1500 zł", "12 500 zł").
/// </summary>
public static class PolishFormat
{
    // U+00A0 written as an escape: in the source it would look like an ordinary space.
    private const char NonBreakingSpace = '\u00A0';

    /// <summary><c>"120.00 PLN"</c> (<c>Price.ToString()</c>) → „120 zł”, „99,50 zł”; another currency – as it is.</summary>
    public static string Price(string value)
    {
        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0
            || !decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            || (parts.Length > 1 && parts[1] != "PLN"))
        {
            return value.Trim();
        }

        var number = amount == decimal.Truncate(amount) ? Number(amount, "0") : Number(amount, "0.00");
        return $"{number}{NonBreakingSpace}zł";
    }

    /// <summary>Kilometres (<c>"2.46"</c>) → „2,5 km”; below 1 km – metres („350 m”), from 10 km – whole km.</summary>
    public static string Distance(string value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var km)
            || !double.IsFinite(km) || km < 0)
        {
            return string.Empty;
        }

        var meters = Math.Max(10, Math.Round(km * 1000 / 10, MidpointRounding.AwayFromZero) * 10);
        if (meters < 1000)
        {
            return $"{meters.ToString("0", CultureInfo.InvariantCulture)} m";
        }

        var shown = km < 10
            ? Math.Round(km, 1, MidpointRounding.AwayFromZero)
            : Math.Round(km, MidpointRounding.AwayFromZero);
        return $"{Number((decimal)shown, "0.#")} km";
    }

    private static string Number(decimal value, string format)
    {
        var text = value.ToString(format, CultureInfo.InvariantCulture).Replace('.', ',');
        if (Math.Abs(value) < 10_000)
        {
            return text;
        }

        // Thousands separated by a non-breaking space (pl-PL), e.g. "12 500".
        var negative = text.StartsWith('-');
        var digits = negative ? text[1..] : text;
        var comma = digits.IndexOf(',');
        var whole = comma < 0 ? digits : digits[..comma];
        var fraction = comma < 0 ? string.Empty : digits[comma..];

        var grouped = new StringBuilder();
        for (var i = 0; i < whole.Length; i++)
        {
            if (i > 0 && (whole.Length - i) % 3 == 0)
            {
                grouped.Append(NonBreakingSpace);
            }

            grouped.Append(whole[i]);
        }

        return (negative ? "-" : string.Empty) + grouped + fraction;
    }
}
