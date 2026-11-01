using Choco.Contract.Abstractions.Messaging;
using Choco.SharedKernel.Results;

using MediatR;

namespace Choco.Application.Abstractions.Messaging;

public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;
