using ICanHelp.Application.Security;
using ICanHelp.Core.Entities.Users;
using Microsoft.AspNetCore.Identity;

namespace ICanHelp.Infrastructure.Security;

/// <summary>
/// Używa <see cref="PasswordHasher{TUser}"/> z ASP.NET Core Identity (PBKDF2-HMAC-SHA512, 100 000 iteracji, losowa sól).
/// Format hasha zawiera wersję, więc w przyszłości można podnieść parametry bez migracji danych.
/// </summary>
internal sealed class PasswordManager(IPasswordHasher<User> passwordHasher) : IPasswordManager
{
    // PasswordHasher nie używa obiektu użytkownika – dlatego null.
    public string Secure(string password) => passwordHasher.HashPassword(null!, password);

    public bool Validate(string password, string passwordHash)
        => passwordHasher.VerifyHashedPassword(null!, passwordHash, password)
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
}
