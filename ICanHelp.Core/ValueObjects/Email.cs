using System.Text.RegularExpressions;
using ICanHelp.Core.Exceptions;

namespace ICanHelp.Core.ValueObjects;

public sealed record Email
{
    private const int MaxLength = 100;

    private static readonly Regex EmailRegex = new(
        @"^(?("")("".+?(?<!\\)""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))" +
        @"(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-0-9a-z]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))$",
        RegexOptions.Compiled);

    public string Value { get; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength)
        {
            throw new InvalidEmailException(value);
        }

        // E-mails are compared case-insensitively, so they are kept in lower case.
        value = value.ToLowerInvariant();
        if (!EmailRegex.IsMatch(value))
        {
            throw new InvalidEmailException(value);
        }

        Value = value;
    }

    public static implicit operator string(Email email) => email.Value;
    public static implicit operator Email(string email) => new(email);
    public override string ToString() => Value;
}
