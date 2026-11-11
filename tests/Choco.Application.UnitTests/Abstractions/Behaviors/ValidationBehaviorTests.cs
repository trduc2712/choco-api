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

    [Fact]
    public async Task Handle_WithInvalidRequest_ReturnsCodeAndMessageForEachFailureInOrder()
    {
        ProfileCommand command = new("", 0, "bio");
        ValidationBehavior<ProfileCommand, Result> behavior = new([new ProfileCommandValidator()]);

        Result result = await behavior.Handle(
            command,
            _ => Task.FromResult(Result.Success()),
            TestContext.Current.CancellationToken);

        IReadOnlyDictionary<string, FieldError[]> errors = result.Error.Should().BeOfType<ValidationError>().Subject.Errors;
        errors.Keys.Should().BeEquivalentTo(nameof(ProfileCommand.Name), nameof(ProfileCommand.Age), nameof(ProfileCommand.Bio));
        errors[nameof(ProfileCommand.Name)].Select(error => error.Code).Should().Equal("Request.Name.Required");
        errors[nameof(ProfileCommand.Age)].Select(error => error.Code).Should().Equal("Request.Age.OutOfRange");
        errors[nameof(ProfileCommand.Bio)].Select(error => error.Code).Should().Equal("Request.Bio.Invalid", "Request.Bio.Custom");
        errors[nameof(ProfileCommand.Bio)].Select(error => error.Message).Should().Equal("Bio is invalid.", "Bio is custom.");
    }

    private sealed record ProfileCommand(string Name, int Age, string Bio);

    private sealed class ProfileCommandValidator : AbstractValidator<ProfileCommand>
    {
        public ProfileCommandValidator()
        {
            RuleFor(command => command.Name).NotEmpty();
            RuleFor(command => command.Age).InclusiveBetween(1, 3);
            RuleFor(command => command.Bio)
                .Must(_ => false).WithMessage("Bio is invalid.")
                .Must(_ => false).WithErrorCode("Custom").WithMessage("Bio is custom.");
        }
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
