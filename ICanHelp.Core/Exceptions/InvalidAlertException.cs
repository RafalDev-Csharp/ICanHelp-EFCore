namespace ICanHelp.Core.Exceptions;

/// <summary>Wrong data of an announcement alert (radius, points of the route, schedule...) → 400.</summary>
public class InvalidAlertException : CustomExceptions
{
    public InvalidAlertException(string message) : base(message)
    {
    }
}
