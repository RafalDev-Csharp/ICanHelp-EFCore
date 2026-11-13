using ICanHelp.Core.Enums;
using ICanHelp.Core.Events;
using ICanHelp.Core.Exceptions;
using static ICanHelp.Core.Tests.Entities.AnnouncementTestData;

namespace ICanHelp.Core.Tests.Entities;

/// <summary>F11 D: realizacja zlecenia – rozpoczęcie (pomocnik), zgłoszenie wykonania, potwierdzenie i zdarzenia.</summary>
public class AnnouncementJobTests
{
    [Fact]
    public void Start_RaisesJobStarted()
    {
        var announcement = InStatus(AnnouncementStatus.Accepted);
        announcement.ClearDomainEvents();

        announcement.Start(Helper, Now.AddDays(1));

        var e = announcement.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<JobStarted>();
        e.AnnouncementId.ShouldBe(announcement.Id.Value);
        e.OwnerId.ShouldBe(OwnerId.Value);
        e.HelperId.ShouldBe(Helper.Value);
        e.OccurredAt.ShouldBe(Now.AddDays(1));
    }

    [Fact]
    public void ReportDone_ByAcceptedHelper_SetsDoneReportedAtAndKeepsStatus()
    {
        var announcement = InStatus(AnnouncementStatus.InProgress);
        announcement.ClearDomainEvents();

        announcement.ReportDone(Helper, Now.AddDays(1).AddHours(3));

        announcement.Status.ShouldBe(AnnouncementStatus.InProgress);
        announcement.DoneReportedAt.ShouldBe(Now.AddDays(1).AddHours(3));
        var e = announcement.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<JobDoneReported>();
        e.OwnerId.ShouldBe(OwnerId.Value);
        e.HelperId.ShouldBe(Helper.Value);
    }

    [Fact]
    public void ReportDone_Twice_Throws()
    {
        var announcement = InStatus(AnnouncementStatus.InProgress);
        announcement.ReportDone(Helper, Now.AddDays(1));

        Should.Throw<DomainRuleException>(() => announcement.ReportDone(Helper, Now.AddDays(2)));

        announcement.DoneReportedAt.ShouldBe(Now.AddDays(1));
    }

    [Fact]
    public void ReportDone_ByOwnerOrStranger_Throws()
    {
        var announcement = InStatus(AnnouncementStatus.InProgress);

        Should.Throw<DomainRuleException>(() => announcement.ReportDone(OwnerId, Now));
        Should.Throw<DomainRuleException>(() => announcement.ReportDone(Stranger, Now));

        announcement.DoneReportedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData(AnnouncementStatus.Open)]
    [InlineData(AnnouncementStatus.Accepted)]
    [InlineData(AnnouncementStatus.Completed)]
    [InlineData(AnnouncementStatus.Cancelled)]
    public void ReportDone_WhenNotInProgress_Throws(AnnouncementStatus status)
    {
        var announcement = InStatus(status);

        Should.Throw<DomainRuleException>(() => announcement.ReportDone(Helper, Now.AddDays(3)));
    }

    [Theory]
    [InlineData(AnnouncementStatus.Accepted)]
    [InlineData(AnnouncementStatus.InProgress)]
    public void Complete_RaisesJobCompletedForTheAcceptedHelper(AnnouncementStatus status)
    {
        var announcement = InStatus(status);
        announcement.ClearDomainEvents();

        announcement.Complete(OwnerId, Now.AddDays(3));

        var e = announcement.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<JobCompleted>();
        e.AnnouncementId.ShouldBe(announcement.Id.Value);
        e.OwnerId.ShouldBe(OwnerId.Value);
        e.HelperId.ShouldBe(Helper.Value);
        e.OccurredAt.ShouldBe(Now.AddDays(3));
    }

    [Fact]
    public void Complete_AfterReportDone_CompletesAndCanBeRated()
    {
        var announcement = InStatus(AnnouncementStatus.InProgress);
        announcement.ReportDone(Helper, Now.AddDays(1));

        announcement.Complete(OwnerId, Now.AddDays(2));

        announcement.Status.ShouldBe(AnnouncementStatus.Completed);
        announcement.DoneReportedAt.ShouldBe(Now.AddDays(1));
        announcement.AllowsConversationWith(Helper).ShouldBeFalse();
    }
}
