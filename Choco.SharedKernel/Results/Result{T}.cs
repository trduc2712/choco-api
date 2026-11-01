namespace Choco.SharedKernel.Results;

public class Result<T> : Result
{
    private readonly T? _value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    private Result(bool isSuccess, Error? error, T? value) : base(isSuccess, error)
    {
        _value = value;
    }

    public static Result<T> Success(T value)
    {
        return new Result<T>(true, null, value);
    }

    public static new Result<T> Failure(Error error)
    {
        return new Result<T>(false, error, default);
    }
}
