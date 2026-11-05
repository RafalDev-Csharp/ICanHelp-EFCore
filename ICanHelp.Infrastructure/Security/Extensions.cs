using System.Text;
using ICanHelp.Application.Security;
using ICanHelp.Core.Entities.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ICanHelp.Infrastructure.Security;

internal static class Extensions
{
    /// <summary>Hashowanie haseł, wystawianie JWT i uwierzytelnianie żądań (JwtBearer).</summary>
    public static IServiceCollection AddSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                "Auth:Issuer and Auth:Audience are required.")
            .Validate(o => Encoding.UTF8.GetByteCount(o.SigningKey ?? string.Empty) >= 32,
                "Auth:SigningKey must be at least 32 bytes long (set it e.g. with the Auth__SigningKey variable).")
            .Validate(o => o.Expiry > TimeSpan.Zero, "Auth:Expiry must be positive.")
            // Zły config wywala aplikację przy starcie, a nie przy pierwszym logowaniu.
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<IPasswordManager, PasswordManager>();
        services.AddSingleton<IAuthenticator, Authenticator>();
        services.AddScoped<ITokenStorage, TokenStorage>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Parametry walidacji tokenu biorą się z tych samych AuthOptions co wystawianie tokenu.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>>((jwt, authOptions) =>
            {
                var auth = authOptions.Value;

                jwt.MapInboundClaims = false; // "sub" zostaje "sub", "role" zostaje "role"
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = auth.Issuer,
                    ValidateAudience = true,
                    ValidAudience = auth.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = auth.CreateSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = AuthClaims.UserName,
                    RoleClaimType = AuthClaims.Role
                };
            });

        services.AddAuthorization();

        return services;
    }
}
