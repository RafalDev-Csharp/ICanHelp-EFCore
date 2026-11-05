using ICanHelp.Application.DTO;
using ICanHelp.Core.Enums;

namespace ICanHelp.Application.Security;

/// <summary>Wystawia token dostępu (JWT) dla zalogowanego użytkownika.</summary>
public interface IAuthenticator
{
    JwtDto CreateToken(Guid userId, string userName, UserRole role);
}
