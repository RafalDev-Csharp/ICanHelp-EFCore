namespace ICanHelp.Core.Exceptions;

/// <summary>F11 E: the reason of an abandonment / interruption is missing or does not fit (→ 400).</summary>
public class InvalidJobExitException : CustomExceptions
{
    public InvalidJobExitException(string message) : base(message)
    {
    }
}
