using Choco.Application.Comments.GetAdminComments;
using Choco.Contract.Comments;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Comments.GetAdminComments;

public sealed class GetAdminCommentsQueryValidatorTests
{
    private static readonly DateTimeOffset From = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly GetAdminCommentsQuery ValidQuery = new(
        null, null, null, null, null, GetAdminCommentsQuery.DefaultPage, GetAdminCommentsQuery.DefaultPageSize);

    private readonly GetAdminCommentsQueryValidator _validator = new();

    [Fact]
    public void Validate_WithoutFilters_HasNoErrors()
    {
        TestValidationResult<GetAdminCommentsQuery> result = _validator.TestValidate(ValidQuery);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEveryFilter_HasNoErrors()
    {
        GetAdminCommentsQuery query = ValidQuery with
        {
            State = "deleted",
            AuthorId = new Guid("00000000-0000-0000-0000-000000000001"),
            PostId = new Guid("00000000-0000-0000-0000-000000000002"),
            From = From,
            To = From.AddDays(1),
            Q = "words"
        };

        TestValidationResult<GetAdminCommentsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("visible")]
    [InlineData("deleted")]
    public void Validate_WithKnownState_HasNoErrors(string state)
    {
        TestValidationResult<GetAdminCommentsQuery> result = _validator.TestValidate(ValidQuery with { State = state });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Visible")]
    [InlineData("archived")]
    public void Validate_WithUnknownState_HasStateError(string state)
    {
        TestValidationResult<GetAdminCommentsQuery> result = _validator.TestValidate(ValidQuery with { State = state });

        result.ShouldHaveValidationErrorFor(query => query.State).Only();
    }

    [Fact]
    public void Validate_WithEmptyAuthorId_HasAuthorIdError()
    {
        TestValidationResult<GetAdminCommentsQuery> result = _validator.TestValidate(ValidQuery with { AuthorId = Guid.Empty });

        result.ShouldHaveValidationErrorFor(query => query.AuthorId).Only();
    }

    [Fact]
    public void Validate_WithEmptyPostId_HasPostIdError()
    {
        TestValidationResult<GetAdminCommentsQuery> result = _validator.TestValidate(ValidQuery with { PostId = Guid.Empty });

        result.ShouldHaveValidationErrorFor(query => query.PostId).Only();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithToNotAfterFrom_HasToError(int days)
    {
        TestValidationResult<GetAdminCommentsQuery> result = _validator.TestValidate(ValidQuery with { From = From, To = From.AddDays(days) });

        result.ShouldHaveValidationErrorFor(query => query.To).Only();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithPageBelowOne_HasPageError(int page)
    {
        TestValidationResult<GetAdminCommentsQuery> result = _validator.TestValidate(ValidQuery with { Page = page });

        result.ShouldHaveValidationErrorFor(query => query.Page).Only();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(GetAdminCommentsQuery.MaxPageSize)]
    public void Validate_WithPageSizeInRange_HasNoErrors(int pageSize)
    {
        TestValidationResult<GetAdminCommentsQuery> result = _validator.TestValidate(ValidQuery with { PageSize = pageSize });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GetAdminCommentsQuery.MaxPageSize + 1)]
    public void Validate_WithPageSizeOutOfRange_HasPageSizeError(int pageSize)
    {
        TestValidationResult<GetAdminCommentsQuery> result = _validator.TestValidate(ValidQuery with { PageSize = pageSize });

        result.ShouldHaveValidationErrorFor(query => query.PageSize).Only();
    }

    [Fact]
    public void Validate_WithSearchAtMaxLengthAfterTrimming_HasNoErrors()
    {
        GetAdminCommentsQuery query = ValidQuery with { Q = $"  {new string('q', GetAdminCommentsQuery.SearchMaxLength)}  " };

        TestValidationResult<GetAdminCommentsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithSearchOverMaxLength_HasSearchError()
    {
        GetAdminCommentsQuery query = ValidQuery with { Q = new string('q', GetAdminCommentsQuery.SearchMaxLength + 1) };

        TestValidationResult<GetAdminCommentsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Q).Only();
    }
}
