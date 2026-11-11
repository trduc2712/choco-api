using Choco.Application.Abstractions.Auditing;
using Choco.Contract.Abstractions.Messaging;
using Choco.Contract.Feedback;
using Choco.Contract.Notifications;
using Choco.Contract.Posts;
using Choco.Contract.Users;

using ApplicationAssemblyReference = Choco.Application.AssemblyReference;
using ContractAssemblyReference = Choco.Contract.AssemblyReference;

namespace Choco.ArchitectureTests;

public class AuditDescriberTests
{
    private static readonly IReadOnlyDictionary<Type, string> Exclusions = new Dictionary<Type, string>
    {
        [typeof(LikePostCommand)] = "Likes are high volume; their current state is in PostLikes.",
        [typeof(UnlikePostCommand)] = "Likes are high volume; their current state is in PostLikes.",
        [typeof(FollowUserCommand)] = "Follows are high volume; their current state is in UserFollows.",
        [typeof(UnfollowUserCommand)] = "Follows are high volume; their current state is in UserFollows.",
        [typeof(MarkNotificationReadCommand)] = "Changes only the caller's own notification state.",
        [typeof(MarkAllNotificationsReadCommand)] = "Changes only the caller's own notification state.",
        [typeof(CreateFeedbackCommand)] = "Sending feedback is not an action on content; only triage status changes are recorded.",
        [typeof(CreateUserCommand)] = "Sent by the system when a user first appears, so no user is the actor.",
        [typeof(BootstrapAdministratorsCommand)] = "Sent by the system at startup, so no user is the actor."
    };

    private static readonly IReadOnlyList<Type> Commands = ContractAssemblyReference.Assembly.GetTypes()
        .Where(type => type is { IsClass: true, IsAbstract: false }
            && type.GetInterfaces().Any(contract => contract == typeof(ICommand)
                || (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ICommand<>))))
        .ToList();

    private static readonly IReadOnlySet<Type> DescribedCommands = ApplicationAssemblyReference.Assembly.GetTypes()
        .Where(type => type is { IsClass: true, IsAbstract: false })
        .SelectMany(type => type.GetInterfaces())
        .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IAuditDescriber<,>))
        .Select(contract => contract.GetGenericArguments()[0])
        .ToHashSet();

    [Fact]
    public void EveryCommand_HasAnAuditDescriber_OrIsExcluded()
    {
        IReadOnlyList<string> unrecorded = Commands
            .Where(command => !DescribedCommands.Contains(command) && !Exclusions.ContainsKey(command))
            .Select(command => command.Name)
            .Order()
            .ToList();

        Assert.True(
            unrecorded.Count == 0,
            $"Commands with neither an audit describer nor an exclusion: {string.Join(", ", unrecorded)}.");
    }

    [Fact]
    public void ExcludedCommands_HaveNoAuditDescriber()
    {
        IReadOnlyList<string> contradictions = Exclusions.Keys
            .Where(DescribedCommands.Contains)
            .Select(command => command.Name)
            .Order()
            .ToList();

        Assert.True(
            contradictions.Count == 0,
            $"Commands both excluded and described: {string.Join(", ", contradictions)}.");
    }

    [Fact]
    public void ExcludedCommands_AreCommandsOfTheContract()
    {
        IReadOnlyList<string> unknown = Exclusions.Keys
            .Where(command => !Commands.Contains(command))
            .Select(command => command.Name)
            .Order()
            .ToList();

        Assert.True(
            unknown.Count == 0,
            $"Exclusions that are not commands of the contract: {string.Join(", ", unknown)}.");
    }
}
