using Choco.Application.Messages.GetMessage;
using Choco.Contract.Messages;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Messages.GetMessage;

public sealed class GetMessageQueryValidatorTests
{
    private readonly GetMessageQueryValidator _validator = new();

    [Fact]
    public void Validate_WithId_HasNoErrors()
    {
        GetMessageQuery query = new(new Guid("00000000-0000-0000-0000-000000000001"));

        TestValidationResult<GetMessageQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyId_HasIdError()
    {
        GetMessageQuery query = new(Guid.Empty);

        TestValidationResult<GetMessageQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Id).Only();
    }
}
