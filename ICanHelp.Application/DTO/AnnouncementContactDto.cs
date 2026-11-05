namespace ICanHelp.Application.DTO;

/// <summary>Dane kontaktowe drugiej strony – dostępne dopiero po akceptacji oferty.</summary>
public sealed record AnnouncementContactDto
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public ContactDataDto ContactData { get; init; } = new();
}
