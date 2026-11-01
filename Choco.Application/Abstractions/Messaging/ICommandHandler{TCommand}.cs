using Choco.Contract.Abstractions.Messaging;
using Choco.SharedKernel.Results;

using MediatR;

namespace Choco.Application.Abstractions.Messaging;

public interface ICommandHandler<TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand;
