using ICanHelp.Core.Exceptions;

namespace ICanHelp.Application.Exceptions;

public class EmailAlreadyInUseException : CustomExceptions
{
    public string Email { get; }

    public EmailAlreadyInUseException(string email) : base($"Email: {email} is already in use.")
    {
        Email = email;
    }
}
