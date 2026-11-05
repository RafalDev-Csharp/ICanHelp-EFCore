using ICanHelp.Core.Exceptions;

namespace ICanHelp.Application.Exceptions;

public class UserLockedException : CustomExceptions
{
    public UserLockedException() : base("User account is locked.")
    {
    }
}
