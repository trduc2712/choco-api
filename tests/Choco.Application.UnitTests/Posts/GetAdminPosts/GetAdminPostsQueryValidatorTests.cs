using Choco.Application.Posts.GetAdminPosts;
using Choco.Contract.Posts;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Posts.GetAdminPosts;

public sealed class GetAdminPostsQueryValidatorTests
{
    private static readonly DateTimeOffset From = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly GetAdminPostsQuery ValidQuery = new(
        null, null, null, null, GetAdminPostsQuery.DefaultPage, GetAdminPostsQuery.DefaultPageSize);

    private readonly GetAdminPostsQueryValidator _validator = new();

    [Fact]
    public void Validate_WithoutFilters_HasNoErrors()
    {
        TestValidationResult<GetAdminPostsQuery> result = _validator.TestValidate(ValidQuery);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEveryFilter_HasNoErrors()
    {
        GetAdminPostsQuery query = ValidQuery with
        {
            State = "deleted",
            AuthorId = new Guid("00000000-0000-0000-0000-000000000001"),
            From = From,
            To = From.AddDays(1),
            Q = "words"
        };

        TestValidationResult<GetAdminPostsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("visible")]
    [InlineData("deleted")]
    public void Validate_WithKnownState_HasNoErrors(string state)
    {
        TestValidationResult<GetAdminPostsQuery> result = _validator.TestValidate(ValidQuery with { State = state });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Visible")]
    [InlineData("archived")]
    public void Validate_WithUnknownState_HasStateError(string state)
    {
        TestValidationResult<GetAdminPostsQuery> result = _validator.TestValidate(ValidQuery with { State = state });

        result.ShouldHaveValidationErrorFor(query => query.State).Only();
    }

    [Fact]
    public void Validate_WithEmptyAuthorId_HasAuthorIdError()
    {
        TestValidationResult<GetAdminPostsQuery> result = _validator.TestValidate(ValidQuery with { AuthorId = Guid.Empty });

        result.ShouldHaveValidationErrorFor(query => query.AuthorId).Only();
    }

    [Fact]
    public void Validate_WithToAfterFrom_HasNoErrors()
    {
        TestValidationResult<GetAdminPostsQuery> result = _validator.TestValidate(ValidQuery with { From = From, To = From.AddTicks(1) });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithToNotAfterFrom_HasToError(int days)
    {
        TestValidationResult<GetAdminPostsQuery> result = _validator.TestValidate(ValidQuery with { From = From, To = From.AddDays(days) });

        result.ShouldHaveValidationErrorFor(query => query.To).Only();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Validate_WithPageAtOrAboveOne_HasNoErrors(int page)
    {
        TestValidationResult<GetAdminPostsQuery> result = _validator.TestValidate(ValidQuery with { Page = page });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithPageBelowOne_HasPageError(int page)
    {
        TestValidationResult<GetAdminPostsQuery> result = _validator.TestValidate(ValidQuery with { Page = page });

        result.ShouldHaveValidationErrorFor(query => query.Page).Only();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(GetAdminPostsQuery.MaxPageSize)]
    public void Validate_WithPageSizeInRange_HasNoErrors(int pageSize)
    {
        TestValidationResult<GetAdminPostsQuery> result = _validator.TestValidate(ValidQuery with { PageSize = pageSize });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GetAdminPostsQuery.MaxPageSize + 1)]
    public void Validate_WithPageSizeOutOfRange_HasPageSizeError(int pageSize)
    {
        TestValidationResult<GetAdminPostsQuery> result = _validator.TestValidate(ValidQuery with { PageSize = pageSize });

        result.ShouldHaveValidationErrorFor(query => query.PageSize).Only();
    }

    [Fact]
    public void Validate_WithSearchAtMaxLengthAfterTrimming_HasNoErrors()
    {
        GetAdminPostsQuery query = ValidQuery with { Q = $"  {new string('q', GetAdminPostsQuery.SearchMaxLength)}  " };

        TestValidationResult<GetAdminPostsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithSearchOverMaxLength_HasSearchError()
    {
        GetAdminPostsQuery query = ValidQuery with { Q = new string('q', GetAdminPostsQuery.SearchMaxLength + 1) };

        TestValidationResult<GetAdminPostsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Q).Only();
    }
}
