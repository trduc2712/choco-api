using Choco.SharedKernel.Results;

namespace Choco.Application.Abstractions.Auditing;

public interface IAuditDescriber<in TRequest, in TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    Task CaptureAsync(TRequest request, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    Task<AuditDescription> DescribeAsync(TRequest request, TResponse response, CancellationToken cancellationToken);

    async Task<IReadOnlyList<AuditDescription>> DescribeAllAsync(
        TRequest request, TResponse response, CancellationToken cancellationToken)
    {
        return [await DescribeAsync(request, response, cancellationToken)];
    }
}
