namespace ICanHelp.Core.Exceptions;

public class InvalidEmailException : CustomExceptions
{
    public string Email { get; }

    public InvalidEmailException(string email) : base($"Email: {email} is invalid.")
    {
        Email = email;
    }
}
