using Choco.Application.Abstractions.Authentication;
using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.GetUserByUsername;

internal sealed class GetUserByUsernameQueryHandler(ICurrentUser currentUser, IUserRepository userRepository)
    : IQueryHandler<GetUserByUsernameQuery, UserProfileResponse>
{
    public async Task<Result<UserProfileResponse>> Handle(GetUserByUsernameQuery query, CancellationToken cancellationToken)
    {
        Result<Guid> userIdResult = await currentUser.GetIdAsync(cancellationToken);

        if (userIdResult.IsFailure)
        {
            return Result<UserProfileResponse>.Failure(userIdResult.Error);
        }

        User? user = await userRepository.GetByUsernameForReadAsync(query.Username, cancellationToken);

        return user is null
            ? Result<UserProfileResponse>.Failure(UserErrors.NotFound)
            : Result<UserProfileResponse>.Success(user.ToProfileResponse());
    }
}
