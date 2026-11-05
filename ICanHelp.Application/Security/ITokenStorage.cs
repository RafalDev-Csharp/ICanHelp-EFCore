using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Security;

/// <summary>
/// Komendy nic nie zwracają, więc handler logowania odkłada tu token,
/// a kontroler odczytuje go w tym samym żądaniu (rejestracja: scoped).
/// </summary>
public interface ITokenStorage
{
    void Set(JwtDto jwt);

    JwtDto? Get();
}
