using System.Text.Json.Serialization;
using ICanHelp.Application.Abstractions;

namespace ICanHelp.Application.Commands;

/// <summary>Rejestracja nowego użytkownika. Konto zawsze dostaje rolę <c>User</c>.</summary>
public sealed record SignUp : ICommand
{
    /// <summary>Nadaje kontroler (żeby zwrócić adres nowego zasobu) – nie przychodzi od klienta.</summary>
    [JsonIgnore]
    public Guid UserId { get; init; }

    public string UserName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string? Description { get; init; }
}
