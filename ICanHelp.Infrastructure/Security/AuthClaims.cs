namespace ICanHelp.Infrastructure.Security;

/// <summary>
/// Nazwy claimów w tokenie. Mapowanie na długie nazwy Microsoftu jest wyłączone (<c>MapInboundClaims = false</c>),
/// więc w kontrolerze czytamy dokładnie te nazwy (np. <c>User.FindFirst(AuthClaims.UserId)</c>).
/// </summary>
public static class AuthClaims
{
    public const string UserId = "sub";
    public const string UserName = "unique_name";
    public const string Role = "role";
}
