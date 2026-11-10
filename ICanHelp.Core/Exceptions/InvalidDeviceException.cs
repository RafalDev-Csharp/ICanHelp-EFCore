namespace ICanHelp.Core.Exceptions;

/// <summary>Wrong data of a push device (empty / too long token, unknown platform) → 400.</summary>
public class InvalidDeviceException : CustomExceptions
{
    public InvalidDeviceException(string message) : base(message)
    {
    }
}
