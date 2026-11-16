using Choco.Application.Messages.GetConversationMessages;
using Choco.Contract.Messages;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Messages.GetConversationMessages;

public sealed class GetConversationMessagesQueryValidatorTests
{
    private static readonly GetConversationMessagesQuery Valid = new(new Guid("00000000-0000-0000-0000-000000000001"), null, null, GetConversationsQuery.DefaultLimit);
    private static readonly GetConversationMessagesQueryValidator Validator = new();

    [Fact]
    public void Validate_EmptyConversationId_RejectsConversationId()
    {
        Validator.TestValidate(Valid with { ConversationId = Guid.Empty })
            .ShouldHaveValidationErrorFor(query => query.ConversationId).Only();
    }

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
    [InlineData("YjA")]
    public void Validate_InvalidCursor_RejectsCursor(string cursor)
    {
        Validator.TestValidate(Valid with { Cursor = cursor }).ShouldHaveValidationErrorFor(query => query.Cursor).Only();
    }

    [Fact]
    public void Validate_CursorCombinedWithAfterSequence_RejectsCursor()
    {
        Validator.TestValidate(Valid with { Cursor = "YjEy", AfterSequence = 0 })
            .ShouldHaveValidationErrorFor(query => query.Cursor).Only();
    }

    [Fact]
    public void Validate_NegativeAfterSequence_RejectsAfterSequence()
    {
        Validator.TestValidate(Valid with { AfterSequence = -1 })
            .ShouldHaveValidationErrorFor(query => query.AfterSequence).Only();
    }

    [Theory]
    [InlineData(null, 0L, 1)]
    [InlineData("YjEy", null, GetConversationsQuery.MaxLimit)]
    [InlineData("YTEy", null, GetConversationsQuery.MaxLimit)]
    public void Validate_ValidPaging_AcceptsQuery(string? cursor, long? afterSequence, int limit)
    {
        Validator.TestValidate(Valid with { Cursor = cursor, AfterSequence = afterSequence, Limit = limit })
            .ShouldNotHaveAnyValidationErrors();
    }
}
