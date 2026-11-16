using Choco.Application.Messages;
using Choco.Application.Messages.GetConversations;
using Choco.Contract.Messages;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Messages.GetConversations;

public sealed class GetConversationsQueryValidatorTests
{
    private static readonly GetConversationsQuery Valid = new(null, GetConversationsQuery.DefaultLimit);
    private static readonly GetConversationsQueryValidator Validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(GetConversationsQuery.MaxLimit + 1)]
    public void Validate_LimitOutsidePageRange_RejectsLimit(int limit)
    {
        Validator.TestValidate(Valid with { Limit = limit }).ShouldHaveValidationErrorFor(query => query.Limit).Only();
    }

    [Theory]
    [InlineData("")]
    [InlineData("!")]
    [InlineData("YjEy")]
    public void Validate_InvalidCursor_RejectsCursor(string cursor)
    {
        Validator.TestValidate(Valid with { Cursor = cursor }).ShouldHaveValidationErrorFor(query => query.Cursor).Only();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(GetConversationsQuery.MaxLimit)]
    public void Validate_ValidCursorAndPageLimit_AcceptsQuery(int limit)
    {
        string cursor = new ConversationCursor(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new Guid("00000000-0000-0000-0000-000000000001")).Encode();

        Validator.TestValidate(Valid with { Cursor = cursor, Limit = limit }).ShouldNotHaveAnyValidationErrors();
    }
}
