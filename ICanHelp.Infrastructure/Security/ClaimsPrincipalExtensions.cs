using System.Security.Claims;

namespace ICanHelp.Infrastructure.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Id użytkownika z claimu <c>sub</c>; <c>false</c>, gdy żądanie jest anonimowe. Jedno miejsce, z którego korzysta
    /// i uwierzytelnianie (token bez poprawnego id jest odrzucany), i Api (kontrolery, hub).
    /// </summary>
    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId)
        => Guid.TryParse(principal.FindFirst(AuthClaims.UserId)?.Value, out userId);
}
