namespace ICanHelp.Core.Exceptions;

/// <summary>The thing can exist only once and it already does (taken e-mail, second rating of the same job...) → 409.</summary>
public abstract class AlreadyExistsException : CustomExceptions
{
    protected AlreadyExistsException(string message) : base(message)
    {
    }
}
