using System.Security.Claims;
using ICanHelp.Infrastructure.Security;

namespace ICanHelp.Api.Security;

internal static class ClaimsPrincipalExtensions
{
    /// <summary>Id zalogowanego użytkownika z claimu <c>sub</c>. Komendy biorą je stąd, nigdy z body.</summary>
    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId)
        => Guid.TryParse(principal.FindFirst(AuthClaims.UserId)?.Value, out userId);
}
