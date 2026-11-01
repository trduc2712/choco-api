using Choco.Contract.Abstractions.Messaging;
using Choco.SharedKernel.Results;

using MediatR;

namespace Choco.Application.Abstractions.Messaging;

public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
