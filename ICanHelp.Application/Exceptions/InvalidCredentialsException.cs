using ICanHelp.Core.Exceptions;

namespace ICanHelp.Application.Exceptions;

/// <summary>Zły e-mail albo hasło. Celowo nie mówimy, które – żeby nie dało się sprawdzać, czy konto istnieje.</summary>
public class InvalidCredentialsException : CustomExceptions
{
    public InvalidCredentialsException() : base("Invalid credentials.")
    {
    }
}
