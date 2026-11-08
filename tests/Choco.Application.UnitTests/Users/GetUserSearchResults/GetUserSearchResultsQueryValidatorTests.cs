using Choco.Application.Users;
using Choco.Application.Users.GetUserSearchResults;
using Choco.Contract.Users;
using Choco.Domain.Users;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Users.GetUserSearchResults;

public sealed class GetUserSearchResultsQueryValidatorTests
{
    private static readonly UserSearchMatch Match =
        new(Guid.Parse("00000000-0000-0000-0000-000000000001"), "username-1", "Display Name", null, 2);

    private readonly GetUserSearchResultsQueryValidator _validator = new();

    [Fact]
    public void Validate_WithCursorForTrimmedSearchText_AcceptsQuery()
    {
        string cursor = UserSearchCursor.Encode(Match, "username");

        _validator.TestValidate(new GetUserSearchResultsQuery(" username ", cursor)).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithCursorForDifferentSearchText_RejectsCursor()
    {
        string cursor = UserSearchCursor.Encode(Match, "username");

        _validator.TestValidate(new GetUserSearchResultsQuery("display name", cursor))
            .ShouldHaveValidationErrorFor(query => query.Cursor).Only();
    }

    [Fact]
    public void Validate_WithMalformedCursor_RejectsCursor()
    {
        _validator.TestValidate(new GetUserSearchResultsQuery("username", "!"))
            .ShouldHaveValidationErrorFor(query => query.Cursor).Only();
    }

    [Fact]
    public void Validate_WithTextOverLimit_RejectsSearchText()
    {
        string text = new('u', User.DisplayNameMaxLength + 1);

        _validator.TestValidate(new GetUserSearchResultsQuery(text, null))
            .ShouldHaveValidationErrorFor(query => query.Q).Only();
    }
}
