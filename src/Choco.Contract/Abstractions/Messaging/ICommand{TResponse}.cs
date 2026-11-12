using Choco.SharedKernel.Results;

using MediatR;

namespace Choco.Contract.Abstractions.Messaging;

public interface ICommand<TResponse> : IRequest<Result<TResponse>>, IBaseCommand;
