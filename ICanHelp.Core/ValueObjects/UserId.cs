using ICanHelp.Core.Exceptions;

namespace ICanHelp.Core.ValueObjects;

public sealed record UserId
{
    public Guid Value { get; }

    public UserId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new InvalidIdentityException(value);
        }

        Value = value;
    }

    public static UserId New() => new(Guid.NewGuid());

    public static implicit operator Guid(UserId id) => id.Value;
    public static implicit operator UserId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
