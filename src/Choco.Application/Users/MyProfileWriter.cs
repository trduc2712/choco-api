using Choco.Application.Abstractions.Authentication;
using Choco.Contract.Users;
using Choco.Domain.Primitives.Persistence;
using Choco.Domain.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users;

internal sealed class MyProfileWriter(ICurrentUser currentUser, IUserRepository users, IUnitOfWork unitOfWork)
{
    public async Task<Result<UserProfileResponse>> UpdateAsync(Func<User, Task<Result>> update, CancellationToken cancellationToken)
    {
        Result<Guid> caller = await currentUser.GetIdAsync(cancellationToken);
        if (caller.IsFailure)
        {
            return Result<UserProfileResponse>.Failure(caller.Error);
        }

        return await unitOfWork.InTransactionAsync(async () =>
        {
            User? user = await users.GetByIdForProfileUpdateAsync(caller.Value, cancellationToken);
            if (user is null)
            {
                return Result<UserProfileResponse>.Failure(UserErrors.Deleted);
            }

            Result result = await update(user);
            if (result.IsFailure)
            {
                return Result<UserProfileResponse>.Failure(result.Error);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            UserProfileDetails? profile = await users.GetProfileByIdForReadAsync(user.Id, caller.Value, cancellationToken);
            return profile is null
                ? Result<UserProfileResponse>.Failure(UserErrors.Deleted)
                : Result<UserProfileResponse>.Success(profile.ToProfileResponse());
        }, cancellationToken);
    }
}
