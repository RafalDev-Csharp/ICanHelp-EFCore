using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities.Users;

/// <summary>
/// Contact details of a user. It has no identity of its own: it lives inside <see cref="User"/>
/// (stored in the same table) and is compared by value.
/// </summary>
public sealed record ContactData(
    Email Email,
    PhoneNumber PhoneNumber,
    StringObject Country,
    StringObject City,
    StringObject PostalCode);
