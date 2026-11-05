using ICanHelp.Core.Exceptions;

namespace ICanHelp.Core.ValueObjects;

public sealed record Id
{
    public Guid Value { get; }

    public Id(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new InvalidIdentityException(value);
        }

        Value = value;
    }

    public static Id New() => new(Guid.NewGuid());

    public static implicit operator Guid(Id id) => id.Value;
    public static implicit operator Id(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
