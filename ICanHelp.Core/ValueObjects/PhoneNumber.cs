using System.Text.RegularExpressions;
using ICanHelp.Core.Exceptions;

namespace ICanHelp.Core.ValueObjects;

public sealed record PhoneNumber
{
    // Optional '+', then 8-15 digits (E.164), so the value is at most 16 characters long.
    private static readonly Regex PhoneRegex = new(@"^\+?[1-9][0-9]{7,14}$", RegexOptions.Compiled);

    public string Value { get; }

    public PhoneNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !PhoneRegex.IsMatch(value))
        {
            throw new InvalidPhoneNumberException(value);
        }

        Value = value;
    }

    public static implicit operator string(PhoneNumber phoneNumber) => phoneNumber.Value;
    public static implicit operator PhoneNumber(string phoneNumber) => new(phoneNumber);
    public override string ToString() => Value;
}
