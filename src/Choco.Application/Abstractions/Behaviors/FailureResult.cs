using System.Reflection;

using Choco.SharedKernel.Results;

namespace Choco.Application.Abstractions.Behaviors;

internal static class FailureResult
{
    public static TResponse Create<TResponse>(Error error) where TResponse : Result
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)Result.Failure(error);
        }

        MethodInfo failure = typeof(TResponse).GetMethod(nameof(Result.Failure), [typeof(Error)])!;

        return (TResponse)failure.Invoke(null, [error])!;
    }
}
