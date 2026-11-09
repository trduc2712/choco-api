using Choco.Application.Comments.GetPostComments;
using Choco.Application.Posts;
using Choco.Contract.Comments;
using Choco.Contract.Posts;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Comments.GetPostComments;

public sealed class GetPostCommentsQueryValidatorTests
{
    private static readonly GetPostCommentsQuery ValidQuery = new(new Guid("00000000-0000-0000-0000-000000000001"), null, GetMyPostsQuery.DefaultLimit);

    private readonly GetPostCommentsQueryValidator _validator = new();

    [Fact]
    public void Validate_WithEmptyPostId_HasPostIdError()
    {
        GetPostCommentsQuery query = ValidQuery with { PostId = Guid.Empty };

        TestValidationResult<GetPostCommentsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.PostId).Only();
    }

    [Fact]
    public void Validate_WithoutCursor_HasNoErrors()
    {
        TestValidationResult<GetPostCommentsQuery> result = _validator.TestValidate(ValidQuery);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithValidCursor_HasNoErrors()
    {
        string cursor = new PostCursor(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new Guid("3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e6f")).Encode();
        GetPostCommentsQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetPostCommentsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(GetMyPostsQuery.MaxLimit)]
    public void Validate_WithLimitAtBound_HasNoErrors(int limit)
    {
        GetPostCommentsQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetPostCommentsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GetMyPostsQuery.MaxLimit + 1)]
    public void Validate_WithLimitOutOfRange_HasLimitError(int limit)
    {
        GetPostCommentsQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetPostCommentsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Limit).Only();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a cursor")]
    public void Validate_WithInvalidCursor_HasCursorError(string cursor)
    {
        GetPostCommentsQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetPostCommentsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Cursor).Only();
    }
}
