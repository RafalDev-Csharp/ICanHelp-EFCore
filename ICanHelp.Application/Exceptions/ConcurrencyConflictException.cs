using ICanHelp.Core.Exceptions;

namespace ICanHelp.Application.Exceptions;

/// <summary>
/// Ktoś inny zmienił ten sam zasób w tej samej chwili (np. właściciel edytuje ogłoszenie, gdy pomocnik akceptuje ofertę).
/// Zmiana nie została zapisana – klient powinien odświeżyć dane i spróbować ponownie (→ 409).
/// </summary>
public class ConcurrencyConflictException : CustomExceptions
{
    public ConcurrencyConflictException()
        : base("The data was changed by someone else in the meantime. Refresh it and try again.")
    {
    }
}
