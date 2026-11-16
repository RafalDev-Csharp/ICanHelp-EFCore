using ICanHelp.Core.Exceptions;

namespace ICanHelp.Application.Exceptions;

/// <summary>Za długa fraza wyszukiwania ogłoszeń (błędne dane → 400).</summary>
public class InvalidSearchPhraseException : CustomExceptions
{
    public InvalidSearchPhraseException(string message) : base(message)
    {
    }
}
