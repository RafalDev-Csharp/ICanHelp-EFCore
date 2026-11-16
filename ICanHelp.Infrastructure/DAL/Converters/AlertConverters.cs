using System.Text.Json;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ICanHelp.Infrastructure.DAL.Converters;

// Lists of values of an alert (Krok 14) stored as JSON in one column – they are always read and written
// with the whole alert, and the matching query filters by them in memory (geometry is filtered by SQL).

/// <summary>Points as <c>[[lat,lon],[lat,lon]...]</c>.</summary>
internal sealed class LocationListConverter() : ValueConverter<IReadOnlyList<Location>, string>(
    points => AlertJson.SerializePoints(points),
    json => AlertJson.DeserializePoints(json));

/// <summary>Ids as <c>["guid","guid"]</c>.</summary>
internal sealed class IdListConverter() : ValueConverter<IReadOnlyList<Id>, string>(
    ids => AlertJson.SerializeIds(ids),
    json => AlertJson.DeserializeIds(json));

/// <summary>Lists compared by content (EF detects a change only when the items differ).</summary>
internal sealed class ListComparer<T>() : ValueComparer<IReadOnlyList<T>>(
    (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b),
    list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
    list => list.ToArray());

internal static class AlertJson
{
    public static string SerializePoints(IReadOnlyList<Location> points)
        => JsonSerializer.Serialize(points.Select(p => new[] { p.Latitude, p.Longitude }));

    public static IReadOnlyList<Location> DeserializePoints(string json)
        => (JsonSerializer.Deserialize<double[][]>(json) ?? [])
            .Select(p => new Location(p[0], p[1]))
            .ToArray();

    public static string SerializeIds(IReadOnlyList<Id> ids)
        => JsonSerializer.Serialize(ids.Select(id => id.Value));

    public static IReadOnlyList<Id> DeserializeIds(string json)
        => (JsonSerializer.Deserialize<Guid[]>(json) ?? [])
            .Select(id => new Id(id))
            .ToArray();
}
