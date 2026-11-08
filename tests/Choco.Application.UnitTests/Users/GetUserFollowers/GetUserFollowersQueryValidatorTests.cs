using Choco.Application.Users;
using Choco.Application.Users.GetUserFollowers;
using Choco.Contract.Users;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Users.GetUserFollowers;

public sealed class GetUserFollowersQueryValidatorTests
{
    private static readonly GetUserFollowersQuery ValidQuery = new(new Guid("00000000-0000-0000-0000-000000000001"), null, GetUserFollowersQuery.DefaultLimit);

    private readonly GetUserFollowersQueryValidator _validator = new();

    [Fact]
    public void Validate_WithEmptyUserId_HasUserIdError()
    {
        GetUserFollowersQuery query = ValidQuery with { UserId = Guid.Empty };

        TestValidationResult<GetUserFollowersQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.UserId).Only();
    }

    [Fact]
    public void Validate_WithoutCursor_HasNoErrors()
    {
        TestValidationResult<GetUserFollowersQuery> result = _validator.TestValidate(ValidQuery);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithValidCursor_HasNoErrors()
    {
        string cursor = new FollowCursor(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new Guid("3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e6f")).Encode();
        GetUserFollowersQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetUserFollowersQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(GetUserFollowersQuery.MaxLimit)]
    public void Validate_WithLimitAtBound_HasNoErrors(int limit)
    {
        GetUserFollowersQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetUserFollowersQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GetUserFollowersQuery.MaxLimit + 1)]
    public void Validate_WithLimitOutOfRange_HasLimitError(int limit)
    {
        GetUserFollowersQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetUserFollowersQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Limit).Only();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a cursor")]
    public void Validate_WithInvalidCursor_HasCursorError(string cursor)
    {
        GetUserFollowersQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetUserFollowersQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Cursor).Only();
    }
}
