using Choco.Application.Posts;
using Choco.Application.Posts.GetFollowingFeed;
using Choco.Contract.Posts;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Posts.GetFollowingFeed;

public sealed class GetFollowingFeedQueryValidatorTests
{
    private static readonly GetFollowingFeedQuery ValidQuery = new(null, GetMyPostsQuery.DefaultLimit);

    private readonly GetFollowingFeedQueryValidator _validator = new();

    [Fact]
    public void Validate_WithoutCursor_HasNoErrors()
    {
        TestValidationResult<GetFollowingFeedQuery> result = _validator.TestValidate(ValidQuery);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithValidCursor_HasNoErrors()
    {
        string cursor = new PostCursor(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new Guid("3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e6f")).Encode();
        GetFollowingFeedQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetFollowingFeedQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(GetMyPostsQuery.MaxLimit)]
    public void Validate_WithLimitAtBound_HasNoErrors(int limit)
    {
        GetFollowingFeedQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetFollowingFeedQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GetMyPostsQuery.MaxLimit + 1)]
    public void Validate_WithLimitOutOfRange_HasLimitError(int limit)
    {
        GetFollowingFeedQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetFollowingFeedQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Limit).Only();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a cursor")]
    public void Validate_WithInvalidCursor_HasCursorError(string cursor)
    {
        GetFollowingFeedQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetFollowingFeedQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Cursor).Only();
    }
}
