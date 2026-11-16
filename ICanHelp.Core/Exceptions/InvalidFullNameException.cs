namespace ICanHelp.Core.Exceptions;

public class InvalidFullNameException : CustomExceptions
{
    public string FullName { get; }

    public InvalidFullNameException(string fullName) : base($"Full name: {fullName} is incorrect.")
    {
        FullName = fullName;
    }
}
