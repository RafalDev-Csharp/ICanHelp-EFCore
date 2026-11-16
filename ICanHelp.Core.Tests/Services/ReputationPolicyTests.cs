using ICanHelp.Core.Services;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Tests.Services;

/// <summary>F11 H: jedna osoba = jeden głos (średnia jej ocen), podejrzane tempo (szybkie potwierdzenie, powtórka z tym samym partnerem).</summary>
public class ReputationPolicyTests
{
    private static readonly DateTime Day0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static FinishedJob Job(UserId partner, DateTime completedAt, TimeSpan? duration = null)
        => new(Id.New(), partner, completedAt - (duration ?? TimeSpan.FromHours(3)), completedAt);

    private static ReceivedRating Rated(FinishedJob job, int rate)
        => new(job.AnnouncementId, job.PartnerId, rate);

    private static RoleReputation Summarize(IReadOnlyCollection<FinishedJob> jobs, params ReceivedRating[] ratings)
        => ReputationPolicy.Summarize(jobs, ratings, ReputationPolicy.FindSuspiciousJobs(jobs));

    [Fact]
    public void DifferentPartners_AllCount()
    {
        var jobs = new[] { Job(UserId.New(), Day0), Job(UserId.New(), Day0.AddDays(1)) };

        var result = Summarize(jobs, Rated(jobs[0], 5), Rated(jobs[1], 4));

        result.AverageRating.ShouldBe(4.5);
        result.RatingCount.ShouldBe(2);
        result.RaterCount.ShouldBe(2);
        result.CompletedCount.ShouldBe(2);
        result.PartnerCount.ShouldBe(2);
        result.MostJobsWithOnePartner.ShouldBe(1);
    }

    [Fact]
    public void SamePartnerManyTimes_CountsAsOneVote_TheAverageOfHisRatings()
    {
        var friend = UserId.New();
        var other = UserId.New();
        var jobs = new[] { Job(friend, Day0), Job(friend, Day0.AddDays(10)), Job(other, Day0) };

        // Przyjaciel: 2 i 4 → jeden głos 3; drugi: 5 → (3 + 5) / 2 = 4 (a nie (2 + 4 + 5) / 3).
        var result = Summarize(jobs, Rated(jobs[0], 2), Rated(jobs[1], 4), Rated(jobs[2], 5));

        result.AverageRating.ShouldBe(4);
        result.RatingCount.ShouldBe(3);
        result.RaterCount.ShouldBe(2);
        result.PartnerCount.ShouldBe(2);
        result.MostJobsWithOnePartner.ShouldBe(2);
    }

    [Fact]
    public void ManyJobsFromOnePerson_WeighAsMuchAsOneJobFromAnother()
    {
        var friend = UserId.New();
        var other = UserId.New();
        var jobs = Enumerable.Range(0, 10).Select(i => Job(friend, Day0.AddDays(i * 10))).Append(Job(other, Day0)).ToList();

        var ratings = jobs.Take(10).Select(j => Rated(j, 5)).Append(Rated(jobs[10], 1)).ToArray();
        var result = Summarize(jobs, ratings);

        result.AverageRating.ShouldBe(3);
        result.RatingCount.ShouldBe(11);
        result.RaterCount.ShouldBe(2);
    }

    [Fact]
    public void ConfirmedQuicklyAfterAcceptance_DoesNotCount()
    {
        var jobs = new[] { Job(UserId.New(), Day0, TimeSpan.FromMinutes(29)) };

        var result = Summarize(jobs, Rated(jobs[0], 5));

        ReputationPolicy.FindSuspiciousJobs(jobs).ShouldContain(jobs[0].AnnouncementId);
        result.AverageRating.ShouldBeNull();
        result.RatingCount.ShouldBe(1);
        result.RaterCount.ShouldBe(0);
        result.CompletedCount.ShouldBe(1);
    }

    [Fact]
    public void ExactlyMinDuration_Counts()
    {
        var jobs = new[] { Job(UserId.New(), Day0, ReputationPolicy.MinJobDuration) };

        ReputationPolicy.FindSuspiciousJobs(jobs).ShouldBeEmpty();
    }

    [Fact]
    public void NextJobWithSamePartnerWithinWindow_DoesNotCount_EarlierStays()
    {
        var friend = UserId.New();
        var jobs = new[] { Job(friend, Day0), Job(friend, Day0.AddDays(6)) };

        var suspicious = ReputationPolicy.FindSuspiciousJobs(jobs);
        var result = ReputationPolicy.Summarize(jobs, [Rated(jobs[0], 3), Rated(jobs[1], 5)], suspicious);

        suspicious.ShouldBe([jobs[1].AnnouncementId]);
        // Nowsza ocena jest podejrzana – głos przyjaciela to tylko wcześniejsza ocena.
        result.AverageRating.ShouldBe(3);
        result.RaterCount.ShouldBe(1);
    }

    [Fact]
    public void NextJobWithSamePartnerAfterWindow_Counts()
    {
        var friend = UserId.New();
        var jobs = new[] { Job(friend, Day0), Job(friend, Day0 + ReputationPolicy.RepeatWindow) };

        ReputationPolicy.FindSuspiciousJobs(jobs).ShouldBeEmpty();
    }

    [Fact]
    public void SameMoment_OnlyOneIsTheRepeat()
    {
        var friend = UserId.New();
        var jobs = new[] { Job(friend, Day0), Job(friend, Day0) };

        ReputationPolicy.FindSuspiciousJobs(jobs).Count.ShouldBe(1);
    }

    [Fact]
    public void JobsWithoutTimes_AreNotSuspicious()
    {
        var friend = UserId.New();
        var jobs = new[]
        {
            new FinishedJob(Id.New(), friend, null, null),
            new FinishedJob(Id.New(), friend, null, null)
        };

        ReputationPolicy.FindSuspiciousJobs(jobs).ShouldBeEmpty();
    }

    [Fact]
    public void NoJobs_EmptyReputation()
    {
        var result = Summarize([]);

        result.ShouldBe(new RoleReputation(null, 0, 0, 0, 0, 0));
    }
}
