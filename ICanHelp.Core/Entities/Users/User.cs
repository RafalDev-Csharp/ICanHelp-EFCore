using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities.Users;

public class User
{
    public UserId Id { get; private set; }

    public UserName UserName { get; private set; }
    public FullName FullName { get; private set; }

    /// <summary>Hash of the password. The plain password is never stored.</summary>
    public string PasswordHash { get; private set; }

    public UserRole Role { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsLocked { get; private set; }

    public Description? Description { get; private set; }
    public ContactData ContactData { get; private set; }

    // Required by EF Core.
    private User()
    {
    }

    public User(UserId id, UserName userName, FullName fullName, string passwordHash,
        ContactData contactData, DateTime createdAtUtc, UserRole role = UserRole.User)
    {
        EnsurePasswordHash(passwordHash);
        ArgumentNullException.ThrowIfNull(contactData);

        Id = id;
        UserName = userName;
        FullName = fullName;
        PasswordHash = passwordHash;
        ContactData = contactData;
        CreatedAt = createdAtUtc;
        Role = role;
        IsLocked = false;
    }

    public void UpdateProfile(FullName fullName, Description? description, ContactData contactData)
    {
        ArgumentNullException.ThrowIfNull(contactData);

        FullName = fullName;
        Description = description;
        ContactData = contactData;
    }

    public void ChangePasswordHash(string passwordHash)
    {
        EnsurePasswordHash(passwordHash);
        PasswordHash = passwordHash;
    }

    public void ChangeRole(UserRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new DomainRuleException($"Role: {role} is invalid.");
        }

        Role = role;
    }

    public void Lock() => IsLocked = true;

    public void Unlock() => IsLocked = false;

    private static void EnsurePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new InvalidPasswordException();
        }
    }
}
