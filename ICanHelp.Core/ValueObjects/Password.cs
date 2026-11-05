using ICanHelp.Core.Exceptions;

namespace ICanHelp.Core.ValueObjects;

/// <summary>
/// A plain-text password, used only to validate user input (e.g. when registering).
/// It must never be stored: the database keeps only <c>User.PasswordHash</c>.
/// </summary>
public sealed record Password
{
    public string Value { get; }

    public Password(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length is > 200 or < 6)
        {
            throw new InvalidPasswordException();
        }

        Value = value;
    }

    public static implicit operator Password(string value) => new(value);
    public static implicit operator string(Password password) => password.Value;

    // Never print the password (logs, debugger, exception messages).
    public override string ToString() => "********";
}
