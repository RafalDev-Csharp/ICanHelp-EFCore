namespace ICanHelp.Core.Exceptions;

/// <summary>Thrown when a business rule of the domain is violated.</summary>
public class DomainRuleException : CustomExceptions
{
    public DomainRuleException(string message) : base(message)
    {
    }
}
