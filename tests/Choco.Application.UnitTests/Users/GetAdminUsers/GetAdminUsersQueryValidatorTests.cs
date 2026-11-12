using Choco.Application.Users.GetAdminUsers;
using Choco.Contract.Users;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Users.GetAdminUsers;

public sealed class GetAdminUsersQueryValidatorTests
{
    private static readonly GetAdminUsersQuery ValidQuery = new(
        null, null, GetAdminUsersQuery.DefaultPage, GetAdminUsersQuery.DefaultPageSize);

    private readonly GetAdminUsersQueryValidator _validator = new();

    [Fact]
    public void Validate_WithoutFilters_HasNoErrors()
    {
        TestValidationResult<GetAdminUsersQuery> result = _validator.TestValidate(ValidQuery);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("active")]
    [InlineData("suspended")]
    [InlineData("deleted")]
    public void Validate_WithKnownState_HasNoErrors(string state)
    {
        TestValidationResult<GetAdminUsersQuery> result = _validator.TestValidate(ValidQuery with { State = state });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Active")]
    [InlineData("banned")]
    public void Validate_WithUnknownState_HasStateError(string state)
    {
        TestValidationResult<GetAdminUsersQuery> result = _validator.TestValidate(ValidQuery with { State = state });

        result.ShouldHaveValidationErrorFor(query => query.State).Only();
    }

    [Fact]
    public void Validate_WithRoleId_HasNoErrors()
    {
        GetAdminUsersQuery query = ValidQuery with { RoleId = new Guid("00000000-0000-0000-0000-000000000001") };

        TestValidationResult<GetAdminUsersQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyRoleId_HasRoleIdError()
    {
        TestValidationResult<GetAdminUsersQuery> result = _validator.TestValidate(ValidQuery with { RoleId = Guid.Empty });

        result.ShouldHaveValidationErrorFor(query => query.RoleId).Only();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Validate_WithPageAtOrAboveOne_HasNoErrors(int page)
    {
        TestValidationResult<GetAdminUsersQuery> result = _validator.TestValidate(ValidQuery with { Page = page });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithPageBelowOne_HasPageError(int page)
    {
        TestValidationResult<GetAdminUsersQuery> result = _validator.TestValidate(ValidQuery with { Page = page });

        result.ShouldHaveValidationErrorFor(query => query.Page).Only();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(GetAdminUsersQuery.MaxPageSize)]
    public void Validate_WithPageSizeInRange_HasNoErrors(int pageSize)
    {
        TestValidationResult<GetAdminUsersQuery> result = _validator.TestValidate(ValidQuery with { PageSize = pageSize });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GetAdminUsersQuery.MaxPageSize + 1)]
    public void Validate_WithPageSizeOutOfRange_HasPageSizeError(int pageSize)
    {
        TestValidationResult<GetAdminUsersQuery> result = _validator.TestValidate(ValidQuery with { PageSize = pageSize });

        result.ShouldHaveValidationErrorFor(query => query.PageSize).Only();
    }

    [Fact]
    public void Validate_WithSearchAtMaxLengthAfterTrimming_HasNoErrors()
    {
        GetAdminUsersQuery query = ValidQuery with { Q = $"  {new string('q', GetAdminUsersQuery.SearchMaxLength)}  " };

        TestValidationResult<GetAdminUsersQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithSearchOverMaxLength_HasSearchError()
    {
        GetAdminUsersQuery query = ValidQuery with { Q = new string('q', GetAdminUsersQuery.SearchMaxLength + 1) };

        TestValidationResult<GetAdminUsersQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Q).Only();
    }
}
