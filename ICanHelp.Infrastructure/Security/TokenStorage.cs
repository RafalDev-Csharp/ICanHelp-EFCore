using ICanHelp.Application.DTO;
using ICanHelp.Application.Security;

namespace ICanHelp.Infrastructure.Security;

/// <summary>Zwykły obiekt scoped – żyje tyle co jedno żądanie HTTP.</summary>
internal sealed class TokenStorage : ITokenStorage
{
    private JwtDto? _jwt;

    public void Set(JwtDto jwt) => _jwt = jwt;

    public JwtDto? Get() => _jwt;
}
