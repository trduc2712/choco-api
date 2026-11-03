using Choco.SharedKernel.Results;

using MediatR;

namespace Choco.Contract.Abstractions.Messaging;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>;
