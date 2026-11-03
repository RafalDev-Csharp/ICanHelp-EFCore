namespace ICanHelp.Application.DTO;

/// <summary>Public data of a user. Never contains the password hash.</summary>
public sealed record UserDto
{
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public bool IsLocked { get; init; }
    public string? Description { get; init; }
    public ContactDataDto ContactData { get; init; } = new();
}
