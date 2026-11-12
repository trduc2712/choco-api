using Choco.Domain.Reports;
using Choco.SharedKernel.Results;

namespace Choco.Domain.UnitTests.Reports;

public sealed class ReportTests
{
    private static readonly Guid ReporterId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid PostId = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid PostAuthorId = new("00000000-0000-0000-0000-000000000003");
    private static readonly Guid UserId = new("00000000-0000-0000-0000-000000000004");
    private static readonly Guid ResolverId = new("00000000-0000-0000-0000-000000000005");
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ResolvedAt = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateForPost_WithOtherAuthor_ReturnsOpenReport()
    {
        Result<Report> result = Report.CreateForPost(
            ReporterId, PostId, PostAuthorId, ReportReason.Spam, "details", CreatedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.ReporterId.Should().Be(ReporterId);
        result.Value.TargetType.Should().Be(ReportTargetType.Post);
        result.Value.TargetPostId.Should().Be(PostId);
        result.Value.TargetUserId.Should().BeNull();
        result.Value.Reason.Should().Be(ReportReason.Spam);
        result.Value.Details.Should().Be("details");
        result.Value.CreatedAt.Should().Be(CreatedAt);
        result.Value.Status.Should().Be(ReportStatus.Open);
        result.Value.Outcome.Should().BeNull();
        result.Value.ResolvedById.Should().BeNull();
        result.Value.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public void CreateForPost_WithOwnPost_ReturnsSelfReportError()
    {
        Result<Report> result = Report.CreateForPost(
            ReporterId, PostId, ReporterId, ReportReason.Spam, null, CreatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ReportErrors.SelfReport);
    }

    [Fact]
    public void CreateForUser_WithOtherUser_ReturnsOpenReport()
    {
        Result<Report> result = Report.CreateForUser(
            ReporterId, UserId, ReportReason.Harassment, null, CreatedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReporterId.Should().Be(ReporterId);
        result.Value.TargetType.Should().Be(ReportTargetType.User);
        result.Value.TargetPostId.Should().BeNull();
        result.Value.TargetUserId.Should().Be(UserId);
        result.Value.Reason.Should().Be(ReportReason.Harassment);
        result.Value.Details.Should().BeNull();
        result.Value.Status.Should().Be(ReportStatus.Open);
    }

    [Fact]
    public void CreateForUser_WithSameUser_ReturnsSelfReportError()
    {
        Result<Report> result = Report.CreateForUser(
            ReporterId, ReporterId, ReportReason.Harassment, null, CreatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ReportErrors.SelfReport);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void CreateForUser_WithBlankDetails_StoresNull(string? details)
    {
        Result<Report> result = Report.CreateForUser(
            ReporterId, UserId, ReportReason.Other, details, CreatedAt);

        result.Value.Details.Should().BeNull();
    }

    [Fact]
    public void CreateForUser_WithPaddedDetails_TrimsThem()
    {
        Result<Report> result = Report.CreateForUser(
            ReporterId, UserId, ReportReason.Other, "  details  ", CreatedAt);

        result.Value.Details.Should().Be("details");
    }

    [Fact]
    public void CreateForUser_WithDetailsAtLimit_Succeeds()
    {
        string details = new('a', Report.DetailsMaxLength);

        Result<Report> result = Report.CreateForUser(
            ReporterId, UserId, ReportReason.Other, details, CreatedAt);

        result.Value.Details.Should().Be(details);
    }

    [Fact]
    public void CreateForUser_WithDetailsPastLimit_ThrowsArgumentException()
    {
        string details = new('a', Report.DetailsMaxLength + 1);

        Action act = () => Report.CreateForUser(ReporterId, UserId, ReportReason.Other, details, CreatedAt);

        act.Should().Throw<ArgumentException>().WithParameterName("details");
    }

    [Fact]
    public void Resolve_WhenOpen_RecordsOutcomeResolverAndTime()
    {
        Report report = OpenReport();

        report.Resolve(ReportOutcome.ContentRemoved, ResolverId, ResolvedAt);

        report.Status.Should().Be(ReportStatus.Resolved);
        report.Outcome.Should().Be(ReportOutcome.ContentRemoved);
        report.ResolvedById.Should().Be(ResolverId);
        report.ResolvedAt.Should().Be(ResolvedAt);
    }

    [Fact]
    public void Resolve_WhenAlreadyResolved_ChangesNothing()
    {
        Guid otherResolverId = new("00000000-0000-0000-0000-000000000006");
        Report report = OpenReport();
        report.Resolve(ReportOutcome.ContentRemoved, ResolverId, ResolvedAt);

        report.Resolve(ReportOutcome.Dismissed, otherResolverId, ResolvedAt.AddDays(1));

        report.Status.Should().Be(ReportStatus.Resolved);
        report.Outcome.Should().Be(ReportOutcome.ContentRemoved);
        report.ResolvedById.Should().Be(ResolverId);
        report.ResolvedAt.Should().Be(ResolvedAt);
    }

    [Theory]
    [InlineData(ReportOutcome.Dismissed)]
    [InlineData(ReportOutcome.ContentRemoved)]
    [InlineData(ReportOutcome.UserSuspended)]
    public void CheckOutcomeApplies_ForAReportAboutAPost_AcceptsEveryOutcome(ReportOutcome outcome)
    {
        Result result = OpenReport().CheckOutcomeApplies(outcome);

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(ReportOutcome.Dismissed)]
    [InlineData(ReportOutcome.UserSuspended)]
    public void CheckOutcomeApplies_ForAReportAboutAUser_AcceptsDismissedAndUserSuspended(ReportOutcome outcome)
    {
        Report report = Report.CreateForUser(ReporterId, UserId, ReportReason.Harassment, null, CreatedAt).Value;

        Result result = report.CheckOutcomeApplies(outcome);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CheckOutcomeApplies_ContentRemovedForAReportAboutAUser_ReturnsOutcomeNotApplicableError()
    {
        Report report = Report.CreateForUser(ReporterId, UserId, ReportReason.Harassment, null, CreatedAt).Value;

        Result result = report.CheckOutcomeApplies(ReportOutcome.ContentRemoved);

        result.Error.Should().Be(ReportErrors.OutcomeNotApplicable);
    }

    [Theory]
    [InlineData(ReportReason.Spam, "Reported for spam")]
    [InlineData(ReportReason.Harassment, "Reported for harassment")]
    [InlineData(ReportReason.HateSpeech, "Reported for hate speech")]
    [InlineData(ReportReason.Violence, "Reported for violence")]
    [InlineData(ReportReason.Other, "Reported for other reasons")]
    public void SuspensionReason_NamesTheReasonOfTheReportAndNothingElse(ReportReason reason, string expected)
    {
        Report report = Report.CreateForPost(ReporterId, PostId, PostAuthorId, reason, "details written by the reporter", CreatedAt).Value;

        report.SuspensionReason.Should().Be(expected);
    }

    private static Report OpenReport()
    {
        return Report.CreateForPost(ReporterId, PostId, PostAuthorId, ReportReason.Spam, null, CreatedAt).Value;
    }
}
