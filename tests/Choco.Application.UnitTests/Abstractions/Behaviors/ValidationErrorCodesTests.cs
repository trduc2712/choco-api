using Choco.Application.Abstractions.Behaviors;
using Choco.Contract.Comments;
using Choco.Contract.Feedback;
using Choco.Contract.Notifications;
using Choco.Contract.Posts;
using Choco.Contract.Reports;
using Choco.Contract.Users;

namespace Choco.Application.UnitTests.Abstractions.Behaviors;

public sealed class ValidationErrorCodesTests
{
    [Theory]
    [InlineData(typeof(CreatePostCommand), "Post")]
    [InlineData(typeof(CreateCommentCommand), "Comment")]
    [InlineData(typeof(UpdateMyBioCommand), "User")]
    [InlineData(typeof(ResolveReportCommand), "Report")]
    [InlineData(typeof(GetMyNotificationsQuery), "Notification")]
    [InlineData(typeof(CreateFeedbackCommand), "Feedback")]
    [InlineData(typeof(ValidationErrorCodesTests), "Request")]
    public void ResourceOf_WithRequestType_ReturnsSingularFeatureName(Type requestType, string expected)
    {
        ValidationErrorCodes.ResourceOf(requestType).Should().Be(expected);
    }

    [Theory]
    [InlineData("NotNullValidator", "Required")]
    [InlineData("NotEmptyValidator", "Required")]
    [InlineData("MaximumLengthValidator", "TooLong")]
    [InlineData("MinimumLengthValidator", "TooShort")]
    [InlineData("InclusiveBetweenValidator", "OutOfRange")]
    [InlineData("GreaterThanValidator", "OutOfRange")]
    [InlineData("LessThanOrEqualValidator", "OutOfRange")]
    [InlineData("PredicateValidator", "Invalid")]
    [InlineData("AsyncPredicateValidator", "Invalid")]
    [InlineData("EmailValidator", "Email")]
    [InlineData("TooLong", "TooLong")]
    [InlineData("Taken", "Taken")]
    [InlineData("", "Invalid")]
    [InlineData(null, "Invalid")]
    public void ReasonOf_WithFailureCode_ReturnsReason(string? failureCode, string expected)
    {
        ValidationErrorCodes.ReasonOf(failureCode).Should().Be(expected);
    }

    [Theory]
    [InlineData("Content", "NotEmptyValidator", "Post.Content.Required")]
    [InlineData("Content", "TooLong", "Post.Content.TooLong")]
    [InlineData("Items[0].Name", "NotEmptyValidator", "Post.Items.Name.Required")]
    public void Create_WithPropertyAndFailureCode_ReturnsResourceFieldAndReason(
        string propertyName, string failureCode, string expected)
    {
        ValidationErrorCodes.Create(typeof(CreatePostCommand), propertyName, failureCode).Should().Be(expected);
    }
}
