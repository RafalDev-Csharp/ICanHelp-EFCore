namespace ICanHelp.Application.DTO;

/// <summary>Odpowiedź 201: id nowego zasobu (np. oferty, która nie ma własnego adresu GET).</summary>
public sealed record CreatedDto(Guid Id);
