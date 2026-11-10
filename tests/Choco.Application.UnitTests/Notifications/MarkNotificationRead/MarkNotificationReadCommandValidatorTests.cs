using Choco.Application.Notifications.MarkNotificationRead;
using Choco.Contract.Notifications;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Notifications.MarkNotificationRead;

public sealed class MarkNotificationReadCommandValidatorTests
{
    private static readonly MarkNotificationReadCommand ValidCommand = new(new Guid("00000000-0000-0000-0000-000000000001"));
    private readonly MarkNotificationReadCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_HasNoErrors()
    {
        TestValidationResult<MarkNotificationReadCommand> result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyNotificationId_HasNotificationIdError()
    {
        MarkNotificationReadCommand command = ValidCommand with { NotificationId = Guid.Empty };

        TestValidationResult<MarkNotificationReadCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(input => input.NotificationId).Only();
    }
}
