using Choco.Application.Posts;
using Choco.Application.Posts.GetMyPosts;
using Choco.Contract.Posts;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Posts.GetMyPosts;

public sealed class GetMyPostsQueryValidatorTests
{
    private static readonly GetMyPostsQuery ValidQuery = new(null, GetMyPostsQuery.DefaultLimit);

    private readonly GetMyPostsQueryValidator _validator = new();

    [Fact]
    public void Validate_WithoutCursor_HasNoErrors()
    {
        TestValidationResult<GetMyPostsQuery> result = _validator.TestValidate(ValidQuery);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithValidCursor_HasNoErrors()
    {
        string cursor = new PostCursor(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new Guid("3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e6f")).Encode();
        GetMyPostsQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetMyPostsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(GetMyPostsQuery.MaxLimit)]
    public void Validate_WithLimitAtBound_HasNoErrors(int limit)
    {
        GetMyPostsQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetMyPostsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GetMyPostsQuery.MaxLimit + 1)]
    public void Validate_WithLimitOutOfRange_HasLimitError(int limit)
    {
        GetMyPostsQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetMyPostsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Limit).Only();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a cursor")]
    public void Validate_WithInvalidCursor_HasCursorError(string cursor)
    {
        GetMyPostsQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetMyPostsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Cursor).Only();
    }
}
