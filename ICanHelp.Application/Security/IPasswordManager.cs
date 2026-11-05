namespace ICanHelp.Application.Security;

/// <summary>Hashowanie i sprawdzanie haseł. Implementacja w Infrastructure (PBKDF2 z ASP.NET Core Identity).</summary>
public interface IPasswordManager
{
    /// <summary>Zwraca hash hasła (z solą), gotowy do zapisania w <c>User.PasswordHash</c>.</summary>
    string Secure(string password);

    /// <summary>Sprawdza, czy hasło pasuje do zapisanego hasha.</summary>
    bool Validate(string password, string passwordHash);
}
