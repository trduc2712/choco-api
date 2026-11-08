using Choco.Application.Users.GetUserSuggestions;
using Choco.Contract.Users;
using Choco.Domain.Users;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Users.GetUserSuggestions;

public sealed class GetUserSuggestionsQueryValidatorTests
{
    private readonly GetUserSuggestionsQueryValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("u")]
    public void Validate_WithMissingOrShortSearchText_AcceptsQuery(string? text)
    {
        _validator.TestValidate(new GetUserSuggestionsQuery(text)).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithTrimmedTextAtLimit_AcceptsQuery()
    {
        string text = new('u', User.DisplayNameMaxLength);

        _validator.TestValidate(new GetUserSuggestionsQuery($" {text} ")).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithTextOverLimit_RejectsSearchText()
    {
        string text = new('u', User.DisplayNameMaxLength + 1);

        _validator.TestValidate(new GetUserSuggestionsQuery(text)).ShouldHaveValidationErrorFor(query => query.Q).Only();
    }

    [Fact]
    public void Validate_WithNullCharacter_RejectsSearchText()
    {
        _validator.TestValidate(new GetUserSuggestionsQuery("user\0name")).ShouldHaveValidationErrorFor(query => query.Q).Only();
    }
}
