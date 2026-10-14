using FluentAssertions;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Services;

using Xunit;

namespace SmartTorque.Tests.Services;

/// <summary>Tests for the Black-job notice builder (PRD BM-10).</summary>
public class NotificationServiceTests
{
    private readonly NotificationService _service = new();

    private static List<Job> Jobs(params string[] jobNos)
        => jobNos.Select(no => new Job { JobNo = no }).ToList();

    [Fact]
    public void RefusesToBuildANoticeWithoutJobs()
    {
        var act = () => _service.CreateBlackJobNotice([]);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void BuildsOneNoticeNamingTheSingleJob()
    {
        var notice = _service.CreateBlackJobNotice(Jobs("JOB-20261013-0001"));

        notice.Type.Should().Be(NotificationType.BlackJob);
        notice.TargetRole.Should().Be("Worker");
        notice.Message.Should().Contain("JOB-20261013-0001");
        notice.Message.Should().Contain("Black job");
        notice.ReadAt.Should().BeNull();
    }

    [Fact]
    public void PointsAtTheFirstJobSoTheBoardCanLinkToIt()
    {
        var jobs = Jobs("JOB-20261013-0001", "JOB-20261013-0002");

        var notice = _service.CreateBlackJobNotice(jobs);

        // JobId is left null: the ids do not exist until the caller saves.
        notice.JobId.Should().BeNull();
        notice.Job.Should().BeSameAs(jobs[0]);
    }

    [Fact]
    public void SummarisesRatherThanListingEveryJobWhenThereAreMany()
    {
        var jobNos = Enumerable.Range(1, 12).Select(i => $"JOB-20261013-{i:0000}").ToArray();

        var notice = _service.CreateBlackJobNotice(Jobs(jobNos));

        notice.Message.Should().Contain("12 urgent Black jobs");
        notice.Message.Should().Contain("JOB-20261013-0005");
        notice.Message.Should().NotContain("JOB-20261013-0006");
        notice.Message.Should().Contain("7 more");
        notice.Message.Length.Should().BeLessThanOrEqualTo(500, "Message is capped at 500");
    }

    [Fact]
    public void MessageStaysWithinTheColumnLimit()
    {
        var jobNos = Enumerable.Range(1, 400).Select(i => $"JOB-20261013-{i:0000}").ToArray();

        var notice = _service.CreateBlackJobNotice(Jobs(jobNos));

        notice.Message.Length.Should().BeLessThanOrEqualTo(500);
    }
}