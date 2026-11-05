using ICanHelp.Core.Exceptions;

namespace ICanHelp.Application.Exceptions;

/// <summary>Konto z tokenu nie istnieje (np. token wystawiony przed usunięciem konta).</summary>
public class UserNotFoundException : CustomExceptions
{
    public Guid UserId { get; }

    public UserNotFoundException(Guid userId) : base($"User with id: {userId} was not found.")
    {
        UserId = userId;
    }
}
