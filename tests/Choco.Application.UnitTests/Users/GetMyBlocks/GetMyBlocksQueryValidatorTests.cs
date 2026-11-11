using Choco.Application.Users;
using Choco.Application.Users.GetMyBlocks;
using Choco.Contract.Users;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Users.GetMyBlocks;

public sealed class GetMyBlocksQueryValidatorTests
{
    private static readonly GetMyBlocksQuery ValidQuery = new(null, GetMyBlocksQuery.DefaultLimit);

    private readonly GetMyBlocksQueryValidator _validator = new();

    [Fact]
    public void Validate_WithoutCursor_HasNoErrors()
    {
        TestValidationResult<GetMyBlocksQuery> result = _validator.TestValidate(ValidQuery);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithValidCursor_HasNoErrors()
    {
        string cursor = new BlockCursor(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new Guid("3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e6f")).Encode();
        GetMyBlocksQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetMyBlocksQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(GetMyBlocksQuery.MaxLimit)]
    public void Validate_WithLimitAtBound_HasNoErrors(int limit)
    {
        GetMyBlocksQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetMyBlocksQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GetMyBlocksQuery.MaxLimit + 1)]
    public void Validate_WithLimitOutOfRange_HasLimitError(int limit)
    {
        GetMyBlocksQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetMyBlocksQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Limit).Only();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a cursor")]
    public void Validate_WithInvalidCursor_HasCursorError(string cursor)
    {
        GetMyBlocksQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetMyBlocksQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Cursor).Only();
    }
}
