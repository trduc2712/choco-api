using Choco.SharedKernel.Results;

using MediatR;

namespace Choco.Contract.Abstractions.Messaging;

public interface ICommand : IRequest<Result>;
