using Choco.Application.Comments.GetCommentReplies;
using Choco.Application.Posts;
using Choco.Contract.Comments;
using Choco.Contract.Posts;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Comments.GetCommentReplies;

public sealed class GetCommentRepliesQueryValidatorTests
{
    private static readonly GetCommentRepliesQuery ValidQuery = new(new Guid("00000000-0000-0000-0000-000000000001"), null, GetMyPostsQuery.DefaultLimit);

    private readonly GetCommentRepliesQueryValidator _validator = new();

    [Fact]
    public void Validate_WithEmptyCommentId_HasCommentIdError()
    {
        GetCommentRepliesQuery query = ValidQuery with { CommentId = Guid.Empty };

        TestValidationResult<GetCommentRepliesQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.CommentId).Only();
    }

    [Fact]
    public void Validate_WithoutCursor_HasNoErrors()
    {
        TestValidationResult<GetCommentRepliesQuery> result = _validator.TestValidate(ValidQuery);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithValidCursor_HasNoErrors()
    {
        string cursor = new PostCursor(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new Guid("3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e6f")).Encode();
        GetCommentRepliesQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetCommentRepliesQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(GetMyPostsQuery.MaxLimit)]
    public void Validate_WithLimitAtBound_HasNoErrors(int limit)
    {
        GetCommentRepliesQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetCommentRepliesQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GetMyPostsQuery.MaxLimit + 1)]
    public void Validate_WithLimitOutOfRange_HasLimitError(int limit)
    {
        GetCommentRepliesQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetCommentRepliesQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Limit).Only();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a cursor")]
    public void Validate_WithInvalidCursor_HasCursorError(string cursor)
    {
        GetCommentRepliesQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetCommentRepliesQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Cursor).Only();
    }
}
