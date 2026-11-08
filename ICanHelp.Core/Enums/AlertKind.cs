namespace ICanHelp.Core.Enums;

/// <summary>What an announcement alert watches (Krok 14).</summary>
public enum AlertKind
{
    /// <summary>A circle around one point (e.g. home): <c>RadiusKm</c> around <c>Center</c>.</summary>
    Area,

    /// <summary>A corridor along a route (e.g. work → home): <c>RadiusKm</c> on both sides of the line.</summary>
    Route
}
