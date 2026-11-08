using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;
using static ICanHelp.Core.Tests.Entities.AnnouncementTestData;

namespace ICanHelp.Core.Tests.Entities;

/// <summary>Krok 11: wątek negocjacji – kto może go założyć, kto i kiedy może pisać, przeczytane/nieprzeczytane.</summary>
public class ConversationTests
{
    private static Conversation Start(Announcement announcement, UserId? helperId = null)
        => announcement.StartConversation(Id.New(), helperId ?? Helper, Now);

    private static ConversationMessage Send(Conversation conversation, Announcement announcement, UserId senderId,
        int minutesLater = 5, string text = "Czy uszczelka jest na miejscu?")
        => conversation.SendMessage(Id.New(), senderId, text, announcement, Now.AddMinutes(minutesLater));

    // ---------- zakładanie ----------

    [Fact]
    public void StartConversation_ByHelper_LinksOwnerAndHelper()
    {
        var announcement = CreateAnnouncement();

        var conversation = Start(announcement);

        conversation.AnnouncementId.ShouldBe(announcement.Id);
        conversation.ClientId.ShouldBe(OwnerId);
        conversation.HelperId.ShouldBe(Helper);
        conversation.CreatedAt.ShouldBe(Now);
        conversation.Messages.ShouldBeEmpty();
        conversation.ClientLastReadAt.ShouldBeNull();
        conversation.HelperLastReadAt.ShouldBeNull();
    }

    [Fact]
    public void StartConversation_ByOwner_Throws()
    {
        var announcement = CreateAnnouncement();

        Should.Throw<DomainRuleException>(() => Start(announcement, OwnerId));
    }

    [Theory]
    [InlineData(AnnouncementStatus.Accepted)]
    [InlineData(AnnouncementStatus.Cancelled)]
    public void StartConversation_WhenNotOpen_Throws(AnnouncementStatus status)
    {
        var announcement = InStatus(status);

        Should.Throw<DomainRuleException>(() => Start(announcement, OtherHelper));
    }

    // ---------- wiadomości ----------

    [Fact]
    public void SendMessage_ByBothSides_KeepsOrderAndRevision()
    {
        var announcement = CreateAnnouncement();
        var conversation = Start(announcement);

        var question = Send(conversation, announcement, Helper, 5);
        announcement.Update(OwnerId, announcement.CategoryId, announcement.SubcategoryId, announcement.Difficulty,
            announcement.Title, "Kran w kuchni cieknie od tygodnia. Uszczelka jest.", announcement.Budget,
            announcement.Location, Now.AddMinutes(6));
        var answer = Send(conversation, announcement, OwnerId, 7, "Tak, dopisałem w opisie.");

        conversation.Messages.Select(m => m.Id).ShouldBe(new[] { question.Id, answer.Id });
        question.SenderId.ShouldBe(Helper);
        question.AnnouncementRevision.ShouldBe(0);
        answer.SenderId.ShouldBe(OwnerId);
        answer.AnnouncementRevision.ShouldBe(1);
        answer.Text.Value.ShouldBe("Tak, dopisałem w opisie.");
        answer.SentAt.ShouldBe(Now.AddMinutes(7));
    }

    [Fact]
    public void SendMessage_ByStranger_Throws()
    {
        var announcement = CreateAnnouncement();
        var conversation = Start(announcement);

        Should.Throw<DomainRuleException>(() => Send(conversation, announcement, Stranger));
        conversation.Messages.ShouldBeEmpty();
    }

    [Fact]
    public void SendMessage_WithOtherAnnouncement_Throws()
    {
        var announcement = CreateAnnouncement();
        var conversation = Start(announcement);

        Should.Throw<DomainRuleException>(() => Send(conversation, CreateAnnouncement(), Helper));
    }

    [Fact]
    public void SendMessage_TooLong_Throws()
    {
        var announcement = CreateAnnouncement();
        var conversation = Start(announcement);

        Should.Throw<InvalidDescriptionException>(() => Send(conversation, announcement, Helper, text: new string('a', 501)));
    }

    [Theory]
    [InlineData(AnnouncementStatus.Accepted)]
    [InlineData(AnnouncementStatus.InProgress)]
    public void SendMessage_AfterAcceptance_OnlyAcceptedHelpersConversation(AnnouncementStatus status)
    {
        var announcement = CreateAnnouncement();
        var accepted = Start(announcement, Helper);
        var other = Start(announcement, OtherHelper);
        var offer = announcement.AddHelperOffer(Helper);
        announcement.AcceptOffer(offer.Id, OwnerId, Now);
        if (status == AnnouncementStatus.InProgress)
        {
            announcement.Start(OwnerId);
        }

        Send(accepted, announcement, OwnerId);
        Send(accepted, announcement, Helper);
        accepted.Messages.Count.ShouldBe(2);

        Should.Throw<DomainRuleException>(() => Send(other, announcement, OtherHelper));
        Should.Throw<DomainRuleException>(() => Send(other, announcement, OwnerId));
    }

    [Fact]
    public void SendMessage_AfterCompletion_NobodyCanWrite()
    {
        var announcement = CreateAnnouncement();
        var conversation = Start(announcement);
        var offer = announcement.AddHelperOffer(Helper);
        announcement.AcceptOffer(offer.Id, OwnerId, Now);
        Send(conversation, announcement, Helper);
        announcement.Start(OwnerId);
        announcement.Complete(OwnerId);

        Should.Throw<DomainRuleException>(() => Send(conversation, announcement, Helper));
        Should.Throw<DomainRuleException>(() => Send(conversation, announcement, OwnerId));
        conversation.Messages.Count.ShouldBe(1); // historia zostaje do odczytu
        announcement.AllowsConversationWith(Helper).ShouldBeFalse();
    }

    [Fact]
    public void SendMessage_AfterCancel_Throws()
    {
        var announcement = CreateAnnouncement();
        var conversation = Start(announcement);
        announcement.Cancel(OwnerId);

        Should.Throw<DomainRuleException>(() => Send(conversation, announcement, Helper));
    }

    [Fact]
    public void AllowsConversationWith_FollowsStatus()
    {
        var announcement = CreateAnnouncement();
        announcement.AllowsConversationWith(Helper).ShouldBeTrue();
        announcement.AllowsConversationWith(OtherHelper).ShouldBeTrue();

        var offer = announcement.AddHelperOffer(Helper);
        announcement.AcceptOffer(offer.Id, OwnerId, Now);

        announcement.AllowsConversationWith(Helper).ShouldBeTrue();
        announcement.AllowsConversationWith(OtherHelper).ShouldBeFalse();
    }

    // ---------- przeczytane ----------

    [Fact]
    public void Unread_CountsOnlyOtherSidesMessagesAfterLastRead()
    {
        var announcement = CreateAnnouncement();
        var conversation = Start(announcement);

        Send(conversation, announcement, Helper, 5);
        Send(conversation, announcement, Helper, 6);

        conversation.CountUnread(OwnerId).ShouldBe(2);
        conversation.CountUnread(Helper).ShouldBe(0);
        conversation.HelperLastReadAt.ShouldBe(Now.AddMinutes(6)); // wysłanie = przeczytane do teraz

        conversation.MarkAsRead(OwnerId, Now.AddMinutes(10));
        conversation.ClientLastReadAt.ShouldBe(Now.AddMinutes(10));
        conversation.CountUnread(OwnerId).ShouldBe(0);

        Send(conversation, announcement, Helper, 11);
        conversation.CountUnread(OwnerId).ShouldBe(1);
    }

    [Fact]
    public void MarkAsRead_EarlierTime_DoesNotMoveMarkBack()
    {
        var announcement = CreateAnnouncement();
        var conversation = Start(announcement);

        conversation.MarkAsRead(Helper, Now.AddMinutes(10));
        conversation.MarkAsRead(Helper, Now.AddMinutes(5));

        conversation.HelperLastReadAt.ShouldBe(Now.AddMinutes(10));
    }

    [Fact]
    public void MarkAsRead_ByStranger_Throws()
    {
        var conversation = Start(CreateAnnouncement());

        Should.Throw<DomainRuleException>(() => conversation.MarkAsRead(Stranger, Now));
    }

    // ---------- widoczność ofert ----------

    [Fact]
    public void GetOffersVisibleTo_OwnerAll_HelperOwn_OthersNone()
    {
        var announcement = CreateAnnouncement();
        var mine = announcement.AddHelperOffer(Helper);
        var theirs = announcement.AddHelperOffer(OtherHelper, 140m);

        announcement.GetOffersVisibleTo(OwnerId).Select(o => o.Id).ShouldBe(new[] { mine.Id, theirs.Id }, ignoreOrder: true);
        announcement.GetOffersVisibleTo(Helper).ShouldHaveSingleItem().Id.ShouldBe(mine.Id);
        announcement.GetOffersVisibleTo(Stranger).ShouldBeEmpty();
        announcement.GetOffersVisibleTo(null).ShouldBeEmpty();
    }
}
