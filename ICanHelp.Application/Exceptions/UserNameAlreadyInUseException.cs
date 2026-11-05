using ICanHelp.Core.Exceptions;

namespace ICanHelp.Application.Exceptions;

public class UserNameAlreadyInUseException : CustomExceptions
{
    public string UserName { get; }

    public UserNameAlreadyInUseException(string userName) : base($"User name: {userName} is already in use.")
    {
        UserName = userName;
    }
}
