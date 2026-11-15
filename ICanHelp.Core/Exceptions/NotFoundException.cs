namespace ICanHelp.Core.Exceptions;

/// <summary>
/// The resource does not exist – or belongs to somebody else and we do not reveal that it exists (→ 404).
/// A new "not found" error only has to inherit from this class; nothing else needs to know about it.
/// </summary>
public abstract class NotFoundException : CustomExceptions
{
    protected NotFoundException(string message) : base(message)
    {
    }
}
