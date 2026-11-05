using ICanHelp.Application.Security;
using ICanHelp.Core.Entities.Users;
using Microsoft.AspNetCore.Identity;

namespace ICanHelp.Infrastructure.Security;

/// <summary>
/// Używa <see cref="PasswordHasher{TUser}"/> z ASP.NET Core Identity (PBKDF2-HMAC-SHA512, 100 000 iteracji, losowa sól).
/// Format hasha zawiera wersję, więc w przyszłości można podnieść parametry bez migracji danych.
/// </summary>
internal sealed class PasswordManager : IPasswordManager
{
    private readonly IPasswordHasher<User> _passwordHasher;

    public PasswordManager(IPasswordHasher<User> passwordHasher)
    {
        _passwordHasher = passwordHasher;
    }

    // PasswordHasher nie używa obiektu użytkownika – dlatego null.
    public string Secure(string password) => _passwordHasher.HashPassword(null!, password);

    public bool Validate(string password, string passwordHash)
        => _passwordHasher.VerifyHashedPassword(null!, passwordHash, password)
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
}
