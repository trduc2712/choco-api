using Choco.Application.Abstractions.Behaviors;
using Choco.Application.Comments.CreateComment;
using Choco.Application.Feedback.CreateFeedback;
using Choco.Application.Feedback.GetFeedback;
using Choco.Application.Posts.CreatePost;
using Choco.Application.Posts.EditPost;
using Choco.Application.Users.UpdateMyBio;
using Choco.Application.Users.UpdateMyProfile;
using Choco.Contract.Comments;
using Choco.Contract.Feedback;
using Choco.Contract.Posts;
using Choco.Contract.Users;
using Choco.Domain.Comments;
using Choco.Domain.FeedbackEntries;
using Choco.Domain.Posts;
using Choco.Domain.Users;

using FluentValidation;
using FluentValidation.Results;

namespace Choco.Application.UnitTests.Abstractions.Behaviors;

public sealed class ValidatorErrorCodesTests
{
    private static readonly Guid Id = new("00000000-0000-0000-0000-000000000001");
    private static readonly FeedbackContextDto Context = new(
        "/path", "en", "system", "user-agent", new FeedbackViewportDto(1280, 720), "app-version");

    [Fact]
    public void CreatePost_WithBlankContent_ReturnsRequired()
    {
        CodesOf(new CreatePostCommandValidator(), new CreatePostCommand(" "))
            .Should().Equal(("Content", "Post.Content.Required"));
    }

    [Fact]
    public void CreatePost_WithContentOverLimit_ReturnsTooLong()
    {
        CodesOf(new CreatePostCommandValidator(), new CreatePostCommand(new string('c', Post.ContentMaxLength + 1)))
            .Should().Equal(("Content", "Post.Content.TooLong"));
    }

    [Fact]
    public void EditPost_WithContentOverLimit_ReturnsTooLong()
    {
        CodesOf(new EditPostCommandValidator(), new EditPostCommand(Id, new string('c', Post.ContentMaxLength + 1)))
            .Should().Equal(("Content", "Post.Content.TooLong"));
    }

    [Fact]
    public void CreateComment_WithContentOverLimit_ReturnsTooLong()
    {
        CodesOf(new CreateCommentCommandValidator(), new CreateCommentCommand(Id, new string('c', Comment.ContentMaxLength + 1)))
            .Should().Equal(("Content", "Comment.Content.TooLong"));
    }

    [Fact]
    public void UpdateMyBio_WithBioOverLimit_ReturnsTooLong()
    {
        CodesOf(new UpdateMyBioCommandValidator(), new UpdateMyBioCommand(new string('b', User.BioMaxLength + 1)))
            .Should().Equal(("Bio", "User.Bio.TooLong"));
    }

    [Fact]
    public void UpdateMyProfile_WithBlankUsernameAndLongUsername_ReturnsRequiredAndTooLong()
    {
        UpdateMyProfileCommandValidator validator = new();

        CodesOf(validator, new UpdateMyProfileCommand("", "First Name"))
            .Should().Equal(("Username", "User.Username.Required"));
        CodesOf(validator, new UpdateMyProfileCommand(new string('u', User.UsernameMaxLength + 1), "First Name"))
            .Should().Equal(("Username", "User.Username.TooLong"));
    }

    [Fact]
    public void UpdateMyProfile_WithCombinedNameOverLimit_ReturnsTooLong()
    {
        CodesOf(
                new UpdateMyProfileCommandValidator(),
                new UpdateMyProfileCommand("username", new string('f', User.DisplayNameMaxLength - 1), "l"))
            .Should().Equal(("LastName", "User.LastName.TooLong"));
    }

    [Fact]
    public void CreateFeedback_WithShortAndLongMessage_ReturnsTooShortAndTooLong()
    {
        CreateFeedbackCommandValidator validator = new();

        CodesOf(validator, new CreateFeedbackCommand("bug", "short", Context))
            .Should().Equal(("Message", "Feedback.Message.TooShort"));
        CodesOf(validator, new CreateFeedbackCommand("bug", new string('m', FeedbackEntry.MessageMaxLength + 1), Context))
            .Should().Equal(("Message", "Feedback.Message.TooLong"));
    }

    [Fact]
    public void CreateFeedback_WithInvalidCategoryAndNestedContext_ReturnsCodesWithFullFieldPath()
    {
        FeedbackContextDto context = Context with
        {
            Path = "path",
            Locale = "fr",
            Viewport = new FeedbackViewportDto(0, 720)
        };

        CodesOf(new CreateFeedbackCommandValidator(), new CreateFeedbackCommand("unknown", "feedback message", context))
            .Should().BeEquivalentTo(
            [
                ("Category", "Feedback.Category.Invalid"),
                ("Context.Path", "Feedback.Context.Path.Invalid"),
                ("Context.Locale", "Feedback.Context.Locale.Invalid"),
                ("Context.Viewport.Width", "Feedback.Context.Viewport.Width.OutOfRange")
            ]);
    }

    [Fact]
    public void GetFeedback_WithInvalidFilters_ReturnsInvalidAndOutOfRange()
    {
        CodesOf(new GetFeedbackQueryValidator(), new GetFeedbackQuery("New", "Bug", "cursor", 0))
            .Should().BeEquivalentTo(
            [
                ("Limit", "Feedback.Limit.OutOfRange"),
                ("Status", "Feedback.Status.Invalid"),
                ("Category", "Feedback.Category.Invalid"),
                ("Cursor", "Feedback.Cursor.Invalid")
            ]);
    }

    private static List<(string Field, string Code)> CodesOf<TRequest>(IValidator<TRequest> validator, TRequest request)
    {
        ValidationResult result = validator.Validate(request);

        return result.Errors
            .Select(failure => (
                failure.PropertyName,
                ValidationErrorCodes.Create(typeof(TRequest), failure.PropertyName, failure.ErrorCode)))
            .ToList();
    }
}
