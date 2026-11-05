using Choco.Application.Abstractions.Authorization;
using Choco.Application.Abstractions.Behaviors;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.UnitTests.Abstractions.Behaviors;

public sealed class AuthorizationBehaviorTests
{
    private const string GrantedPermission = "granted.permission";
    private const string DeniedPermission = "denied.permission";

    private static readonly IPermissionChecker GrantsOnlyGrantedPermission = new StubPermissionChecker(
        (permission, scope) => Result<bool>.Success(permission == GrantedPermission && scope == PermissionScope.Platform));

    [Fact]
    public async Task Handle_WithoutAuthorizers_ReturnsResultFromNext()
    {
        StubPermissionChecker deniesEverything = new((_, _) => Result<bool>.Success(false));
        AuthorizationBehavior<TestCommand, Result> behavior = new([], deniesEverything);
        Result expected = Result.Success();

        Result result = await behavior.Handle(
            new TestCommand(),
            _ => Task.FromResult(expected),
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task Handle_WithGrantedPermission_ReturnsResultFromNext()
    {
        AuthorizationBehavior<TestCommand, Result> behavior = new(
            [new TestAuthorizer(GrantedPermission)], GrantsOnlyGrantedPermission);
        Result expected = Result.Success();

        Result result = await behavior.Handle(
            new TestCommand(),
            _ => Task.FromResult(expected),
            TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task Handle_WithDeniedPermission_ReturnsPermissionDeniedWithoutCallingNext()
    {
        AuthorizationBehavior<TestCommand, Result> behavior = new(
            [new TestAuthorizer(DeniedPermission)], GrantsOnlyGrantedPermission);
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
        result.Error.Should().Be(PermissionErrors.Denied);
    }

    [Fact]
    public async Task Handle_WhenCallerCannotBeResolved_ReturnsCheckerErrorWithoutCallingNext()
    {
        StubPermissionChecker failsForDeletedUser = new((_, _) => Result<bool>.Failure(UserErrors.Deleted));
        AuthorizationBehavior<TestCommand, Result> behavior = new(
            [new TestAuthorizer(GrantedPermission)], failsForDeletedUser);
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
        result.Error.Should().Be(UserErrors.Deleted);
    }

    [Fact]
    public async Task Handle_WithSecondAuthorizerDenied_ReturnsPermissionDenied()
    {
        AuthorizationBehavior<TestCommand, Result> behavior = new(
            [new TestAuthorizer(GrantedPermission), new TestAuthorizer(DeniedPermission)], GrantsOnlyGrantedPermission);

        Result result = await behavior.Handle(
            new TestCommand(),
            _ => Task.FromResult(Result.Success()),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PermissionErrors.Denied);
    }

    [Fact]
    public async Task Handle_WithDeniedPermissionAndGenericResponse_ReturnsFailedResultOfT()
    {
        AuthorizationBehavior<TestCommand, Result<int>> behavior = new(
            [new TestAuthorizer(DeniedPermission)], GrantsOnlyGrantedPermission);

        Result<int> result = await behavior.Handle(
            new TestCommand(),
            _ => Task.FromResult(Result<int>.Success(1)),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PermissionErrors.Denied);
    }

    private sealed record TestCommand;

    private sealed class TestAuthorizer : IAuthorizer<TestCommand>
    {
        private readonly string _permission;

        public TestAuthorizer(string permission)
        {
            _permission = permission;
        }

        public PermissionRequirement GetRequirement(TestCommand request)
        {
            return new PermissionRequirement(_permission, PermissionScope.Platform);
        }
    }

    private sealed class StubPermissionChecker : IPermissionChecker
    {
        private readonly Func<string, PermissionScope, Result<bool>> _answer;

        public StubPermissionChecker(Func<string, PermissionScope, Result<bool>> answer)
        {
            _answer = answer;
        }

        public Task<Result<bool>> IsGrantedToCurrentUserAsync(
            string permission, PermissionScope scope, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_answer(permission, scope));
        }
    }
}
