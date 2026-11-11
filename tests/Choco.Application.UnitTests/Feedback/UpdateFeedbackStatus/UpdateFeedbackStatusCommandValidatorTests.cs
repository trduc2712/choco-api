using Choco.Application.Feedback.UpdateFeedbackStatus;
using Choco.Contract.Feedback;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Feedback.UpdateFeedbackStatus;

public sealed class UpdateFeedbackStatusCommandValidatorTests
{
    private static readonly Guid FeedbackId = new("00000000-0000-0000-0000-000000000001");
    private readonly UpdateFeedbackStatusCommandValidator _validator = new();

    [Theory]
    [InlineData("new")]
    [InlineData("in_progress")]
    [InlineData("resolved")]
    [InlineData("wont_fix")]
    public void Validate_WithKnownStatus_AcceptsCommand(string status)
    {
        var result = _validator.TestValidate(new UpdateFeedbackStatusCommand(FeedbackId, status));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("New")]
    [InlineData("InProgress")]
    [InlineData("unknown")]
    public void Validate_WithUnknownStatus_RejectsStatus(string? status)
    {
        var result = _validator.TestValidate(new UpdateFeedbackStatusCommand(FeedbackId, status!));

        result.ShouldHaveValidationErrorFor(command => command.Status).Only();
    }

    [Fact]
    public void Validate_WithEmptyId_RejectsId()
    {
        var result = _validator.TestValidate(new UpdateFeedbackStatusCommand(Guid.Empty, "new"));

        result.ShouldHaveValidationErrorFor(command => command.Id).Only();
    }
}
