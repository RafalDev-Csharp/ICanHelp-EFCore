namespace ICanHelp.Core.Exceptions;

/// <summary>
/// Base of all expected errors of the application (wrong data, a broken rule, a missing resource).
/// The API turns them into ProblemDetails; the kind of error comes from the base class:
/// <see cref="NotFoundException"/> → 404, <see cref="DomainRuleException"/> / <see cref="AlreadyExistsException"/> → 409,
/// anything else → 400.
/// </summary>
public abstract class CustomExceptions : Exception
{
    protected CustomExceptions(string message) : base(message)
    {
    }
}
