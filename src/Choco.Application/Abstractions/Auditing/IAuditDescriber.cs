using Choco.SharedKernel.Results;

namespace Choco.Application.Abstractions.Auditing;

public interface IAuditDescriber<in TRequest, in TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    Task<AuditDescription> DescribeAsync(TRequest request, TResponse response, CancellationToken cancellationToken);
}
