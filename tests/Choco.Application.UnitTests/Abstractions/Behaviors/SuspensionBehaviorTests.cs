using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Behaviors;
using Choco.Contract.Abstractions.Messaging;
using Choco.SharedKernel.Results;

namespace Choco.Application.UnitTests.Abstractions.Behaviors;

public sealed class SuspensionBehaviorTests
{
    private static readonly DateTimeOffset SuspendedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly CurrentUserSuspension Suspension = new("reason", SuspendedAt);

    [Fact]
    public async Task Handle_CommandOfSuspendedUser_ReturnsSuspendedErrorWithoutCallingNext()
    {
        StubCurrentUser currentUser = new(Result<CurrentUserSuspension?>.Success(Suspension));
        SuspensionBehavior<TestCommand, Result> behavior = new(currentUser);
        bool nextCalled = false;

        Result result = await behavior.Handle(
            new TestCommand(),
            _ =>
            {
                nextCalled = true;
                return Task.FromResult(Result.Success());
            },
            TestContext.Current.CancellationToken);

        nextCalled.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("User.Suspended");
        result.Error!.Type.Should().Be(ErrorType.Forbidden);
        result.Error!.Description.Should().Contain("reason");
        ErrorWithExtensions error = result.Error!.Should().BeOfType<ErrorWithExtensions>().Subject;
        Dictionary<string, object?> suspension = error.Extensions["suspension"].Should().BeOfType<Dictionary<string, object?>>().Subject;
        suspension["reason"].Should().Be("reason");
        suspension["suspendedAt"].Should().Be(SuspendedAt);
    }

    [Fact]
    public async Task Handle_CommandWithResponseOfSuspendedUser_ReturnsSuspendedErrorInTheResponseType()
    {
        StubCurrentUser currentUser = new(Result<CurrentUserSuspension?>.Success(Suspension));
        SuspensionBehavior<TestCommandWithResponse, Result<string>> behavior = new(currentUser);

        Result<string> result = await behavior.Handle(
            new TestCommandWithResponse(),
            _ => Task.FromResult(Result<string>.Success("value")),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("User.Suspended");
    }

    [Fact]
    public async Task Handle_CommandOfUserWhoIsNotSuspended_ReturnsResultFromNext()
    {
        StubCurrentUser currentUser = new(Result<CurrentUserSuspension?>.Success(null));
        SuspensionBehavior<TestCommand, Result> behavior = new(currentUser);
        Result expected = Result.Success();

        Result result = await behavior.Handle(
            new TestCommand(), _ => Task.FromResult(expected), TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task Handle_QueryOfSuspendedUser_ReturnsResultFromNextWithoutLoadingTheUser()
    {
        StubCurrentUser currentUser = new(Result<CurrentUserSuspension?>.Success(Suspension));
        SuspensionBehavior<TestQuery, Result<string>> behavior = new(currentUser);
        Result<string> expected = Result<string>.Success("value");

        Result<string> result = await behavior.Handle(
            new TestQuery(), _ => Task.FromResult(expected), TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        currentUser.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SystemRequestWhileTheCallerIsSuspended_ReturnsResultFromNextWithoutLoadingTheUser()
    {
        StubCurrentUser currentUser = new(Result<CurrentUserSuspension?>.Success(Suspension));
        SuspensionBehavior<TestSystemCommand, Result> behavior = new(currentUser);
        Result expected = Result.Success();

        Result result = await behavior.Handle(
            new TestSystemCommand(), _ => Task.FromResult(expected), TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        currentUser.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheUserCannotBeLoaded_ReturnsThatFailureWithoutCallingNext()
    {
        Error failure = Error.Unauthorized("Test.Unauthorized", "Test failure.");
        StubCurrentUser currentUser = new(Result<CurrentUserSuspension?>.Failure(failure));
        SuspensionBehavior<TestCommand, Result> behavior = new(currentUser);
        bool nextCalled = false;

        Result result = await behavior.Handle(
            new TestCommand(),
            _ =>
            {
                nextCalled = true;
                return Task.FromResult(Result.Success());
            },
            TestContext.Current.CancellationToken);

        nextCalled.Should().BeFalse();
        result.Error.Should().Be(failure);
    }

    private sealed record TestCommand : ICommand;

    private sealed record TestCommandWithResponse : ICommand<string>;

    private sealed record TestQuery : IQuery<string>;

    private sealed record TestSystemCommand : ICommand, ISystemRequest;

    private sealed class StubCurrentUser(Result<CurrentUserSuspension?> suspension) : ICurrentUser
    {
        public int Calls { get; private set; }

        public Task<Result<Guid>> GetIdAsync(CancellationToken cancellationToken = default)
        {
            Calls++;

            return Task.FromResult(Result<Guid>.Success(Guid.NewGuid()));
        }

        public Task<Result<CurrentUserSuspension?>> GetSuspensionAsync(CancellationToken cancellationToken = default)
        {
            Calls++;

            return Task.FromResult(suspension);
        }
    }
}
