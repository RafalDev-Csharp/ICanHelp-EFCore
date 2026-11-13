using ICanHelp.Application.Notifications;
using ICanHelp.Core.Enums;

namespace ICanHelp.Application.Tests.Notifications;

/// <summary>Krok 15: tytuł i treść powiadomienia (push, NotificationDto) dla każdego typu.</summary>
public class NotificationTextFormatterTests
{
    private const char Nbsp = ' ';
    private const string Title = "Cieknący kran";

    private static NotificationText Format(NotificationType type, params (string Key, string Value)[] parameters)
        => NotificationTextFormatter.Format(type, parameters.ToDictionary(p => p.Key, p => p.Value));

    [Fact]
    public void OfferReceived_TitleWithPrice_BodyAnnouncement()
        => Format(NotificationType.OfferReceived, (NotificationPayload.AnnouncementTitle, Title),
                (NotificationPayload.Price, "150.00 PLN"))
            .ShouldBe(new NotificationText($"Nowa oferta: 150{Nbsp}zł", Title));

    [Fact]
    public void OfferCountered_TitleWithPrice()
        => Format(NotificationType.OfferCountered, (NotificationPayload.AnnouncementTitle, Title),
                (NotificationPayload.Price, "99.50 PLN"))
            .ShouldBe(new NotificationText($"Kontroferta: 99,50{Nbsp}zł", Title));

    [Fact]
    public void OfferRevised_TitleWithPrice()
        => Format(NotificationType.OfferRevised, (NotificationPayload.AnnouncementTitle, Title),
                (NotificationPayload.Price, "180.00 PLN"))
            .ShouldBe(new NotificationText($"Oferta zaktualizowana: 180{Nbsp}zł", Title));

    [Theory]
    [InlineData(NotificationType.JobStarted, "Pomocnik rozpoczął zlecenie")]
    [InlineData(NotificationType.JobDoneReported, "Pomocnik zgłosił wykonanie – potwierdź")]
    [InlineData(NotificationType.JobCompleted, "Zlecenie zakończone – możesz ocenić")]
    public void Job_TitleAndAnnouncement(NotificationType type, string expected)
        => Format(type, (NotificationPayload.AnnouncementTitle, Title)).ShouldBe(new NotificationText(expected, Title));

    [Theory]
    [InlineData(NotificationType.JobStarted)]
    [InlineData(NotificationType.JobDoneReported)]
    [InlineData(NotificationType.JobCompleted)]
    public void Job_LinkToConversation(NotificationType type)
    {
        var conversationId = Guid.NewGuid();
        NotificationTextFormatter.Link(type, Guid.NewGuid(), conversationId).ShouldBe($"/conversations/{conversationId}");
    }

    [Fact]
    public void Offer_WithoutPrice_TitleOnly()
        => Format(NotificationType.OfferReceived, (NotificationPayload.AnnouncementTitle, Title))
            .Title.ShouldBe("Nowa oferta");

    [Fact]
    public void OfferAccepted()
        => Format(NotificationType.OfferAccepted, (NotificationPayload.AnnouncementTitle, Title))
            .ShouldBe(new NotificationText("Twoja oferta została przyjęta", Title));

    [Fact]
    public void OfferRejected_ByOwner()
        => Format(NotificationType.OfferRejected, (NotificationPayload.AnnouncementTitle, Title))
            .Title.ShouldBe("Twoja oferta została odrzucona");

    [Fact]
    public void OfferRejected_AnotherOfferAccepted()
        => Format(NotificationType.OfferRejected, (NotificationPayload.AnnouncementTitle, Title),
                (NotificationPayload.Reason, NotificationPayload.AnotherOfferAccepted))
            .Title.ShouldBe("Zleceniodawca wybrał innego pomocnika");

    [Fact]
    public void OfferWithdrawn()
        => Format(NotificationType.OfferWithdrawn, (NotificationPayload.AnnouncementTitle, Title))
            .ShouldBe(new NotificationText("Oferta wycofana", Title));

    [Fact]
    public void MessageReceived_BodyIsPreview()
        => Format(NotificationType.MessageReceived, (NotificationPayload.AnnouncementTitle, Title),
                (NotificationPayload.MessagePreview, "Mogę jutro o 17?"))
            .ShouldBe(new NotificationText($"Nowa wiadomość: {Title}", "Mogę jutro o 17?"));

    [Fact]
    public void AnnouncementEdited_ListsChangedFieldsInPolish()
        => Format(NotificationType.AnnouncementEdited, (NotificationPayload.AnnouncementTitle, Title),
                (NotificationPayload.ChangedFields, "Title, Budget"))
            .ShouldBe(new NotificationText($"Ogłoszenie zmienione: {Title}",
                "Zmieniono: tytuł, budżet. Sprawdź, czy Twoja oferta jest aktualna."));

    [Fact]
    public void AnnouncementEdited_WithoutFields()
        => Format(NotificationType.AnnouncementEdited, (NotificationPayload.AnnouncementTitle, Title),
                (NotificationPayload.ChangedFields, "None"))
            .Body.ShouldBe("Sprawdź szczegóły ogłoszenia.");

    [Theory]
    [InlineData("Area", "2.5", "Nowe ogłoszenie w okolicy", "2,5 km")]
    [InlineData("Route", "0.35", "Nowe ogłoszenie po drodze", "350 m")]
    [InlineData("Route", "12.7", "Nowe ogłoszenie po drodze", "13 km")]
    [InlineData("Area", "3.0", "Nowe ogłoszenie w okolicy", "3 km")]
    public void NewAnnouncementNearby_WhereAndDistance(string kind, string km, string title, string distance)
        => Format(NotificationType.NewAnnouncementNearby, (NotificationPayload.AnnouncementTitle, Title),
                (NotificationPayload.AlertKind, kind), (NotificationPayload.DistanceKm, km),
                (NotificationPayload.CategoryName, "Hydraulika"), (NotificationPayload.AlertName, "Dom"))
            .ShouldBe(new NotificationText(title, $"{Title} – {distance} · Hydraulika · alert „Dom”"));

    [Fact]
    public void NewAnnouncementsDigest_Count()
        => Format(NotificationType.NewAnnouncementsDigest, (NotificationPayload.Count, "4"))
            .ShouldBe(new NotificationText("Kolejne nowe ogłoszenia (4)",
                "Pasują do Twoich alertów – zobacz listę ogłoszeń."));

    [Fact]
    public void EveryType_HasNonEmptyTitle()
    {
        foreach (var type in Enum.GetValues<NotificationType>())
        {
            Format(type).Title.ShouldNotBeNullOrWhiteSpace(type.ToString());
        }
    }

    [Theory]
    [InlineData("12500.00 PLN", "12 500 zł")]
    [InlineData("1500.00 PLN", "1500 zł")]
    [InlineData("10.00 EUR", "10.00 EUR")]
    [InlineData("abc", "abc")]
    public void FormatPrice(string value, string expected)
        => NotificationTextFormatter.FormatPrice(value).ShouldBe(expected);

    [Fact]
    public void Link_ConversationForOffers_AnnouncementForEditAndAlert_ListForDigest()
    {
        var announcementId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        NotificationTextFormatter.Link(NotificationType.OfferReceived, announcementId, conversationId)
            .ShouldBe($"/conversations/{conversationId}");
        NotificationTextFormatter.Link(NotificationType.MessageReceived, announcementId, null)
            .ShouldBe($"/announcements/{announcementId}");
        NotificationTextFormatter.Link(NotificationType.AnnouncementEdited, announcementId, conversationId)
            .ShouldBe($"/announcements/{announcementId}");
        NotificationTextFormatter.Link(NotificationType.NewAnnouncementNearby, announcementId, null)
            .ShouldBe($"/announcements/{announcementId}");
        NotificationTextFormatter.Link(NotificationType.NewAnnouncementsDigest, null, null)
            .ShouldBe("/announcements");
        NotificationTextFormatter.Link(NotificationType.OfferAccepted, null, null).ShouldBeNull();
    }
}
