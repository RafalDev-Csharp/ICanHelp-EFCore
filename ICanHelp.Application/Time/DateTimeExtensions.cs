namespace ICanHelp.Application.Time;

internal static class DateTimeExtensions
{
    /// <summary>Daty z JSON-a: z "Z" są UTC, z przesunięciem (+01:00) – lokalne, bez strefy – traktujemy jako UTC.</summary>
    public static DateTime AsUtc(this DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
