using Choco.Application.Abstractions.Behaviors;
using Choco.SharedKernel.Results;

using FluentValidation;

namespace Choco.Application.UnitTests.Abstractions.Behaviors;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_WithValidRequest_ReturnsResultFromNext()
    {
        TestCommand command = new("name");
        ValidationBehavior<TestCommand, Result> behavior = new([new TestCommandValidator()]);
        Result expected = Result.Success();

        Result result = await behavior.Handle(
            command,
            _ => Task.FromResult(expected),
            TestContext.Current.CancellationToken);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_WithInvalidRequest_ReturnsValidationErrorWithoutCallingNext()
    {
        TestCommand command = new("");
        ValidationBehavior<TestCommand, Result> behavior = new([new TestCommandValidator()]);
        bool nextCalled = false;

        Result result = await behavior.Handle(
            command,
            _ =>
            {
                nextCalled = true;
                return Task.FromResult(Result.Success());
            },
            TestContext.Current.CancellationToken);

        nextCalled.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ValidationError>()
            .Which.Errors.Should().ContainKey(nameof(TestCommand.Name));
    }

    [Fact]
    public async Task Handle_WithInvalidRequestAndGenericResponse_ReturnsFailedResultOfT()
    {
        TestCommand command = new("");
        ValidationBehavior<TestCommand, Result<int>> behavior = new([new TestCommandValidator()]);

        Result<int> result = await behavior.Handle(
            command,
            _ => Task.FromResult(Result<int>.Success(1)),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ValidationError>();
    }

    private sealed record TestCommand(string Name);

    private sealed class TestCommandValidator : AbstractValidator<TestCommand>
    {
        public TestCommandValidator()
        {
            RuleFor(command => command.Name).NotEmpty();
        }
    }
}
