namespace ICanHelp.Core.Exceptions;

public class InvalidPasswordException : CustomExceptions
{
    public InvalidPasswordException() : base("Invalid password.")
    {
    }
}
