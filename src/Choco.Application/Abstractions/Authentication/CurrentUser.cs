using Choco.Contract.Users;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

using MediatR;

namespace Choco.Application.Abstractions.Authentication;

internal sealed class CurrentUser : ICurrentUser
{
    private readonly IUserContext _userContext;
    private readonly IUserRepository _userRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly ISender _sender;
    private Result<Guid>? _cachedUserIdResult;

    public CurrentUser(
        IUserContext userContext,
        IUserRepository userRepository,
        IIdentityProvider identityProvider,
        ISender sender)
    {
        _userContext = userContext;
        _userRepository = userRepository;
        _identityProvider = identityProvider;
        _sender = sender;
    }

    public async Task<Result<Guid>> GetIdAsync(CancellationToken cancellationToken = default)
    {
        return _cachedUserIdResult ??= await FindOrCreateUserIdAsync(cancellationToken);
    }

    private async Task<Result<Guid>> FindOrCreateUserIdAsync(CancellationToken cancellationToken)
    {
        string identityId = _userContext.IdentityId;

        User? user = await _userRepository.GetByIdentityIdIncludingDeletedAsync(identityId, cancellationToken);

        if (user is null)
        {
            IdpUser? idpUser = await _identityProvider.GetUserAsync(identityId, cancellationToken);

            if (idpUser is null)
            {
                return Result<Guid>.Failure(UserErrors.Deleted);
            }

            Result createUserResult = await _sender.Send(
                new CreateUserCommand(idpUser.IdentityId, idpUser.Username ?? string.Empty, idpUser.FullName, idpUser.AvatarUrl),
                cancellationToken);

            if (createUserResult.IsFailure)
            {
                return Result<Guid>.Failure(createUserResult.Error);
            }

            user = await _userRepository.GetByIdentityIdIncludingDeletedAsync(identityId, cancellationToken);
        }

        return user is null || user.IsDeleted
            ? Result<Guid>.Failure(UserErrors.Deleted)
            : Result<Guid>.Success(user.Id);
    }
}
