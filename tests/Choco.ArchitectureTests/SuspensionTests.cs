using Choco.Contract.Abstractions.Messaging;
using Choco.Contract.Users;

using MediatR;

using ContractAssemblyReference = Choco.Contract.AssemblyReference;

namespace Choco.ArchitectureTests;

public class SuspensionTests
{
    private static readonly Type[] ContractTypes = ContractAssemblyReference.Assembly
        .GetTypes()
        .Where(type => type is { IsClass: true, IsAbstract: false })
        .ToArray();

    [Fact]
    public void SystemRequests_AreOnlyTheOnesThatHaveNoUserBehindThem()
    {
        string[] systemRequests = ContractTypes
            .Where(type => typeof(ISystemRequest).IsAssignableFrom(type))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal([nameof(BootstrapAdministratorsCommand), nameof(CreateUserCommand)], systemRequests);
    }

    [Fact]
    public void EveryCommand_IsMarkedAsACommand_SoThatASuspendedUserIsRefused()
    {
        string[] notMarked = ContractTypes
            .Where(type => type.Name.EndsWith("Command", StringComparison.Ordinal)
                && typeof(IBaseRequest).IsAssignableFrom(type)
                && !typeof(IBaseCommand).IsAssignableFrom(type))
            .Select(type => type.Name)
            .ToArray();

        Assert.Empty(notMarked);
    }

    [Fact]
    public void NoQuery_IsMarkedAsACommand()
    {
        string[] queriesMarkedAsCommands = ContractTypes
            .Where(type => type.Name.EndsWith("Query", StringComparison.Ordinal)
                && typeof(IBaseCommand).IsAssignableFrom(type))
            .Select(type => type.Name)
            .ToArray();

        Assert.Empty(queriesMarkedAsCommands);
    }
}
