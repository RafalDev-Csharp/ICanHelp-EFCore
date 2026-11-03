using ICanHelp.Core.Exceptions;

namespace ICanHelp.Core.ValueObjects;

/// <summary>A whole-number rating from 1 to 5.</summary>
public sealed record Rate
{
    public int Value { get; }

    public Rate(int value)
    {
        if (value is < 1 or > 5)
        {
            throw new DomainRuleException("Rate must be between 1 and 5.");
        }

        Value = value;
    }

    public static implicit operator int(Rate rate) => rate.Value;
    public override string ToString() => Value.ToString();
}
