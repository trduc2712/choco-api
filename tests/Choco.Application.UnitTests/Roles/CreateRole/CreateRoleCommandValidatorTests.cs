using Choco.Application.Roles.CreateRole;
using Choco.Contract.Roles;
using Choco.Domain.Roles;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Roles.CreateRole;

public sealed class CreateRoleCommandValidatorTests
{
    private static readonly CreateRoleCommand ValidCommand = new("Name", [Permission.ReadAuditLog], null, RoleColors.Teal, RoleIcons.Shield);

    private readonly CreateRoleCommandValidator _validator = new();

    [Fact]
    public void Validate_DescriptionAtTrimmedLimit_Passes()
    {
        CreateRoleCommand command = ValidCommand with { Description = $"  {new string('d', Role.DescriptionMaxLength)}  " };

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_DescriptionAboveTrimmedLimit_Fails()
    {
        CreateRoleCommand command = ValidCommand with { Description = new string('d', Role.DescriptionMaxLength + 1) };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(item => item.Description).Only();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-color")]
    public void Validate_UnsupportedColor_Fails(string? color)
    {
        CreateRoleCommand command = ValidCommand with { Color = color! };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(item => item.Color).Only();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-icon")]
    public void Validate_UnsupportedIcon_Fails(string? icon)
    {
        CreateRoleCommand command = ValidCommand with { Icon = icon! };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(item => item.Icon).Only();
    }

    [Fact]
    public void Validate_WithValidCommand_HasNoErrors()
    {
        TestValidationResult<CreateRoleCommand> result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithoutPermissions_HasNoErrors()
    {
        TestValidationResult<CreateRoleCommand> result = _validator.TestValidate(ValidCommand with { Permissions = [] });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEveryPermissionOfTheCatalog_HasNoErrors()
    {
        TestValidationResult<CreateRoleCommand> result = _validator.TestValidate(ValidCommand with { Permissions = [.. Permission.All] });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithNameAtMaxLength_HasNoErrors()
    {
        CreateRoleCommand command = ValidCommand with { Name = new string('n', Role.NameMaxLength) };

        TestValidationResult<CreateRoleCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithNameAtMaxLengthAfterTrimming_HasNoErrors()
    {
        CreateRoleCommand command = ValidCommand with { Name = $"  {new string('n', Role.NameMaxLength)}  " };

        TestValidationResult<CreateRoleCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithNameOverMaxLength_HasNameError()
    {
        CreateRoleCommand command = ValidCommand with { Name = new string('n', Role.NameMaxLength + 1) };

        TestValidationResult<CreateRoleCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Name).Only();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WithBlankName_HasNameError(string? name)
    {
        CreateRoleCommand command = ValidCommand with { Name = name! };

        TestValidationResult<CreateRoleCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Name).Only();
    }

    [Fact]
    public void Validate_WithoutPermissionList_HasPermissionsError()
    {
        CreateRoleCommand command = ValidCommand with { Permissions = null! };

        TestValidationResult<CreateRoleCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Permissions).Only();
    }

    [Theory]
    [InlineData("unknown.permission")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WithPermissionOutsideTheCatalog_HasPermissionsError(string? permission)
    {
        CreateRoleCommand command = ValidCommand with { Permissions = [Permission.ReadAuditLog, permission!] };

        TestValidationResult<CreateRoleCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Permissions).Only();
    }
}
