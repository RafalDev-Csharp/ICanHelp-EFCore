namespace ICanHelp.Core.Enums;

/// <summary>What the notification is about – the client builds the text from it and the parameters.</summary>
public enum NotificationType
{
    /// <summary>A helper made an offer on my announcement.</summary>
    OfferReceived,

    /// <summary>The other side answered my offer with a counter-offer.</summary>
    OfferCountered,

    /// <summary>The other side accepted my offer.</summary>
    OfferAccepted,

    /// <summary>The other side rejected my offer (or the owner chose another helper).</summary>
    OfferRejected,

    /// <summary>The other side withdrew its offer.</summary>
    OfferWithdrawn,

    /// <summary>A new message in my conversation.</summary>
    MessageReceived,

    /// <summary>The owner changed an announcement I am negotiating about.</summary>
    AnnouncementEdited,

    /// <summary>Krok 14: a new announcement matches one of my alerts (area / route).</summary>
    NewAnnouncementNearby,

    /// <summary>Krok 14: over the daily limit of alerts – one digest a day ("and N more new announcements").</summary>
    NewAnnouncementsDigest,

    /// <summary>F11 C/D: the other side changed its pending offer (new terms, also an outdated one brought up to date).</summary>
    OfferRevised,

    /// <summary>F11 D: the accepted helper started the work on my announcement.</summary>
    JobStarted,

    /// <summary>F11 D: the helper reported the work as done – confirm it.</summary>
    JobDoneReported,

    /// <summary>F11 D: the owner confirmed the work – the job is completed, I can rate.</summary>
    JobCompleted,

    /// <summary>F11 E: the other side abandoned the job before the start (with the reason).</summary>
    JobAbandoned,

    /// <summary>F11 E: the other side interrupted the work (with the reason) – I can rate.</summary>
    JobInterrupted
}
