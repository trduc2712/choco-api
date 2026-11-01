using Choco.SharedKernel.Results;

namespace Choco.SharedKernel.UnitTests.Results;

public sealed class ResultOfTTests
{
    [Fact]
    public void Value_OnFailedResult_ThrowsInvalidOperationException()
    {
        Error error = Error.Failure("Error.Code", "Error description");
        Result<int> result = Result<int>.Failure(error);

        Func<int> act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Success_WithValue_ReturnsSuccessfulResultWithThatValue()
    {
        int value = 1;

        Result<int> result = Result<int>.Success(value);

        result.Value.Should().Be(value);
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_WithError_ReturnsFailedResultWithThatError()
    {
        Error error = Error.Failure("Error.Code", "Error description");

        Result<int> result = Result<int>.Failure(error);

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
    }
}
