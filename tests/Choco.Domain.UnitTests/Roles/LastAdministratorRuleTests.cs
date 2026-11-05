using Choco.Domain.Roles;
using Choco.SharedKernel.Results;

namespace Choco.Domain.UnitTests.Roles;

public sealed class LastAdministratorRuleTests
{
    private static readonly Role AdministratorRole =
        Role.CreateSystem(SystemRoles.All.Single(definition => definition.Key == SystemRoles.Admin));

    private static readonly Role OtherRole =
        Role.CreateSystem(new SystemRoleDefinition("other-role", "Other Role", IsDefault: false, Permissions: []));

    [Fact]
    public void Check_LastHolderOfAdministratorRole_ReturnsLastAdministratorError()
    {
        Result result = LastAdministratorRule.Check(AdministratorRole, userHoldsRole: true, holderCount: 1);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RoleErrors.LastAdministrator);
    }

    [Fact]
    public void Check_AdministratorRoleWithAnotherHolder_ReturnsSuccess()
    {
        Result result = LastAdministratorRule.Check(AdministratorRole, userHoldsRole: true, holderCount: 2);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Check_UserWithoutAdministratorRole_ReturnsSuccess()
    {
        Result result = LastAdministratorRule.Check(AdministratorRole, userHoldsRole: false, holderCount: 1);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Check_LastHolderOfOtherRole_ReturnsSuccess()
    {
        Result result = LastAdministratorRule.Check(OtherRole, userHoldsRole: true, holderCount: 1);

        result.IsSuccess.Should().BeTrue();
    }
}
