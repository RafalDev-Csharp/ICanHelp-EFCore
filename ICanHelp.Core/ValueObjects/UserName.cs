using ICanHelp.Core.Exceptions;

namespace ICanHelp.Core.ValueObjects;

public sealed record UserName
{
    public string Value { get; }

    public UserName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length is > 30 or < 3)
        {
            throw new InvalidStringObjectException(value);
        }

        Value = value;
    }

    public static implicit operator UserName(string value) => new(value);
    public static implicit operator string(UserName userName) => userName.Value;
    public override string ToString() => Value;
}
