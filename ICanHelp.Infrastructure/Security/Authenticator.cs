using ICanHelp.Application.DTO;
using ICanHelp.Application.Security;
using ICanHelp.Application.Time;
using ICanHelp.Core.Enums;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ICanHelp.Infrastructure.Security;

internal sealed class Authenticator : IAuthenticator
{
    private readonly AuthOptions _options;
    private readonly IClock _clock;
    private readonly SigningCredentials _signingCredentials;
    private readonly JsonWebTokenHandler _tokenHandler = new();

    public Authenticator(IOptions<AuthOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
        _signingCredentials = new SigningCredentials(_options.CreateSigningKey(), SecurityAlgorithms.HmacSha256);
    }

    public JwtDto CreateToken(Guid userId, string userName, UserRole role)
    {
        var now = _clock.UtcNow;
        var expires = now.Add(_options.Expiry);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = _signingCredentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = userId.ToString(),
                [JwtRegisteredClaimNames.UniqueName] = userName,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                [AuthClaims.Role] = role.ToString()
            }
        };

        return new JwtDto
        {
            AccessToken = _tokenHandler.CreateToken(descriptor),
            ExpiresAtUtc = expires
        };
    }
}
