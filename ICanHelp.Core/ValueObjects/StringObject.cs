using ICanHelp.Core.Exceptions;

namespace ICanHelp.Core.ValueObjects;

/// <summary>A short, required text (names, titles, file names). Compared by value.</summary>
public sealed record StringObject
{
    public string Value { get; }

    public StringObject(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100)
        {
            throw new InvalidStringObjectException(value);
        }

        Value = value;
    }

    public static implicit operator StringObject(string value) => new(value);
    public static implicit operator string(StringObject stringObject) => stringObject.Value;
    public override string ToString() => Value;
}
