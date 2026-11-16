using Choco.Application.Messages.GetConversationByUser;
using Choco.Contract.Messages;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Messages.GetConversationByUser;

public sealed class GetConversationByUserQueryValidatorTests
{
    private static readonly GetConversationByUserQuery Valid = new(new Guid("00000000-0000-0000-0000-000000000001"));
    private static readonly GetConversationByUserQueryValidator Validator = new();

    [Fact]
    public void Validate_EmptyUserId_RejectsUserId()
    {
        Validator.TestValidate(Valid with { UserId = Guid.Empty }).ShouldHaveValidationErrorFor(query => query.UserId).Only();
    }

    [Fact]
    public void Validate_ValidUserId_AcceptsQuery()
    {
        Validator.TestValidate(Valid).ShouldNotHaveAnyValidationErrors();
    }
}
