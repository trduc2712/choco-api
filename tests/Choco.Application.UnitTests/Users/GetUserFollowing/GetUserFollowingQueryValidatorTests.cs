using Choco.Application.Users;
using Choco.Application.Users.GetUserFollowing;
using Choco.Contract.Users;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Users.GetUserFollowing;

public sealed class GetUserFollowingQueryValidatorTests
{
    private static readonly GetUserFollowingQuery ValidQuery = new(new Guid("00000000-0000-0000-0000-000000000001"), null, GetUserFollowersQuery.DefaultLimit);

    private readonly GetUserFollowingQueryValidator _validator = new();

    [Fact]
    public void Validate_WithEmptyUserId_HasUserIdError()
    {
        GetUserFollowingQuery query = ValidQuery with { UserId = Guid.Empty };

        TestValidationResult<GetUserFollowingQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.UserId).Only();
    }

    [Fact]
    public void Validate_WithoutCursor_HasNoErrors()
    {
        TestValidationResult<GetUserFollowingQuery> result = _validator.TestValidate(ValidQuery);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithValidCursor_HasNoErrors()
    {
        string cursor = new FollowCursor(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new Guid("3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e6f")).Encode();
        GetUserFollowingQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetUserFollowingQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(GetUserFollowersQuery.MaxLimit)]
    public void Validate_WithLimitAtBound_HasNoErrors(int limit)
    {
        GetUserFollowingQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetUserFollowingQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GetUserFollowersQuery.MaxLimit + 1)]
    public void Validate_WithLimitOutOfRange_HasLimitError(int limit)
    {
        GetUserFollowingQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetUserFollowingQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Limit).Only();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a cursor")]
    public void Validate_WithInvalidCursor_HasCursorError(string cursor)
    {
        GetUserFollowingQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetUserFollowingQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Cursor).Only();
    }
}
