using ICanHelp.Core.Exceptions;

namespace ICanHelp.Application.Exceptions;

/// <summary>Niepełne albo błędne parametry wyszukiwania po odległości (błędne dane → 400).</summary>
public class InvalidDistanceFilterException : CustomExceptions
{
    public InvalidDistanceFilterException(string message) : base(message)
    {
    }
}
