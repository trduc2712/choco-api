using Choco.Application.Users.GetUserFollowers;
using Choco.Contract.Users;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Users.GetUserFollowers;

public sealed class GetUserFollowersQueryValidatorTests
{
    private static readonly GetUserFollowersQuery ValidQuery = new(
        new Guid("00000000-0000-0000-0000-000000000001"), null, GetUserFollowersQuery.DefaultLimit);
    private readonly GetUserFollowersQueryValidator _validator = new();

    [Theory]
    [InlineData(1, null)]
    [InlineData(GetUserFollowersQuery.MaxLimit, "00000000000000000000000000000002")]
    public void Validate_WithValidPage_HasNoErrors(int limit, string? cursor)
    {
        TestValidationResult<GetUserFollowersQuery> result = _validator.TestValidate(ValidQuery with { Limit = limit, Cursor = cursor });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyUserId_HasUserIdError()
    {
        TestValidationResult<GetUserFollowersQuery> result = _validator.TestValidate(ValidQuery with { UserId = Guid.Empty });

        result.ShouldHaveValidationErrorFor(query => query.UserId).Only();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GetUserFollowersQuery.MaxLimit + 1)]
    public void Validate_WithLimitOutsideRange_HasLimitError(int limit)
    {
        TestValidationResult<GetUserFollowersQuery> result = _validator.TestValidate(ValidQuery with { Limit = limit });

        result.ShouldHaveValidationErrorFor(query => query.Limit).Only();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-cursor")]
    public void Validate_WithInvalidCursor_HasCursorError(string cursor)
    {
        TestValidationResult<GetUserFollowersQuery> result = _validator.TestValidate(ValidQuery with { Cursor = cursor });

        result.ShouldHaveValidationErrorFor(query => query.Cursor).Only();
    }
}
