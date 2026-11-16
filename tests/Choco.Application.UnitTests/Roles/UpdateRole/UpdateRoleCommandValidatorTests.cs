using Choco.Application.Roles.UpdateRole;
using Choco.Contract.Roles;
using Choco.Domain.AuditLogs;
using Choco.Domain.Roles;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Roles.UpdateRole;

public sealed class UpdateRoleCommandValidatorTests
{
    private static readonly UpdateRoleCommand ValidCommand =
        new(new Guid("00000000-0000-0000-0000-000000000001"), "Name", [Permission.ReadAuditLog], null, RoleColors.Teal, RoleIcons.Shield);

    private readonly UpdateRoleCommandValidator _validator = new();

    [Fact]
    public void Validate_ReasonAtLimit_Passes()
    {
        UpdateRoleCommand command = ValidCommand with { Reason = new string('r', AuditLogEntry.ReasonMaxLength) };

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ReasonAboveLimit_Fails()
    {
        UpdateRoleCommand command = ValidCommand with { Reason = new string('r', AuditLogEntry.ReasonMaxLength + 1) };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(item => item.Reason).Only();
    }

    [Fact]
    public void Validate_DescriptionAtTrimmedLimit_Passes()
    {
        UpdateRoleCommand command = ValidCommand with { Description = $"  {new string('d', Role.DescriptionMaxLength)}  " };

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_DescriptionAboveTrimmedLimit_Fails()
    {
        UpdateRoleCommand command = ValidCommand with { Description = new string('d', Role.DescriptionMaxLength + 1) };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(item => item.Description).Only();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-color")]
    public void Validate_UnsupportedColor_Fails(string? color)
    {
        UpdateRoleCommand command = ValidCommand with { Color = color! };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(item => item.Color).Only();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-icon")]
    public void Validate_UnsupportedIcon_Fails(string? icon)
    {
        UpdateRoleCommand command = ValidCommand with { Icon = icon! };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(item => item.Icon).Only();
    }

    [Fact]
    public void Validate_WithValidCommand_HasNoErrors()
    {
        TestValidationResult<UpdateRoleCommand> result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithoutPermissions_HasNoErrors()
    {
        TestValidationResult<UpdateRoleCommand> result = _validator.TestValidate(ValidCommand with { Permissions = [] });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyId_HasIdError()
    {
        TestValidationResult<UpdateRoleCommand> result = _validator.TestValidate(ValidCommand with { Id = Guid.Empty });

        result.ShouldHaveValidationErrorFor(c => c.Id).Only();
    }

    [Fact]
    public void Validate_WithNameAtMaxLengthAfterTrimming_HasNoErrors()
    {
        UpdateRoleCommand command = ValidCommand with { Name = $"  {new string('n', Role.NameMaxLength)}  " };

        TestValidationResult<UpdateRoleCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithNameOverMaxLength_HasNameError()
    {
        UpdateRoleCommand command = ValidCommand with { Name = new string('n', Role.NameMaxLength + 1) };

        TestValidationResult<UpdateRoleCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Name).Only();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WithBlankName_HasNameError(string? name)
    {
        TestValidationResult<UpdateRoleCommand> result = _validator.TestValidate(ValidCommand with { Name = name! });

        result.ShouldHaveValidationErrorFor(c => c.Name).Only();
    }

    [Fact]
    public void Validate_WithoutPermissionList_HasPermissionsError()
    {
        TestValidationResult<UpdateRoleCommand> result = _validator.TestValidate(ValidCommand with { Permissions = null! });

        result.ShouldHaveValidationErrorFor(c => c.Permissions).Only();
    }

    [Theory]
    [InlineData("unknown.permission")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WithPermissionOutsideTheCatalog_HasPermissionsError(string? permission)
    {
        UpdateRoleCommand command = ValidCommand with { Permissions = [Permission.ReadAuditLog, permission!] };

        TestValidationResult<UpdateRoleCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Permissions).Only();
    }
}
