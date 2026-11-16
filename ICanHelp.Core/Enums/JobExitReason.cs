namespace ICanHelp.Core.Enums;

/// <summary>F11 E: why a side abandoned / interrupted a job (the reason is required, <see cref="Other"/> also needs a note).</summary>
public enum JobExitReason
{
    /// <summary>„Nie mogę w tym terminie”.</summary>
    CannotMakeIt = 1,

    /// <summary>„Warunki inne niż w ogłoszeniu”.</summary>
    TermsDiffer = 2,

    /// <summary>„Brak kontaktu”.</summary>
    NoContact = 3,

    /// <summary>„Konflikt / nie dogadaliśmy się” – only during the work (interruption).</summary>
    Disagreement = 4,

    /// <summary>„Inny” – the note says what.</summary>
    Other = 5
}
