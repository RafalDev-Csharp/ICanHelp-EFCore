using ICanHelp.Core.Exceptions;

namespace ICanHelp.Core.ValueObjects;

public sealed record Description
{
    public string Value { get; }

    public Description(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length is > 500 or < 2)
        {
            throw new InvalidDescriptionException(value);
        }

        Value = value;
    }

    public static implicit operator Description(string value) => new(value);
    public static implicit operator string(Description description) => description.Value;
    public override string ToString() => Value;
}
