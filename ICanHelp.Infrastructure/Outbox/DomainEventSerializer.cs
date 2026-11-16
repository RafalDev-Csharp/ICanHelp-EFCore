using System.Text.Json;
using System.Text.Json.Serialization;
using ICanHelp.Core.Events;

namespace ICanHelp.Infrastructure.Outbox;

/// <summary>
/// Event ↔ (type name, JSON). The type is the class name only ("OfferMade"), so moving an event to another namespace
/// does not break waiting messages; renaming an event class does – keep the old name or migrate the rows.
/// </summary>
internal static class DomainEventSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly IReadOnlyDictionary<string, Type> Types = typeof(IDomainEvent).Assembly
        .GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IDomainEvent).IsAssignableFrom(t))
        .ToDictionary(t => t.Name);

    public static OutboxMessage ToOutboxMessage(IDomainEvent domainEvent)
    {
        var type = domainEvent.GetType();
        return new OutboxMessage(Guid.NewGuid(), type.Name, JsonSerializer.Serialize(domainEvent, type, Options),
            domainEvent.OccurredAt);
    }

    public static IDomainEvent Deserialize(string typeName, string payload)
    {
        if (!Types.TryGetValue(typeName, out var type))
        {
            throw new InvalidOperationException($"Unknown domain event type: {typeName}.");
        }

        return (IDomainEvent)(JsonSerializer.Deserialize(payload, type, Options)
                              ?? throw new InvalidOperationException($"Empty payload of {typeName}."));
    }
}
