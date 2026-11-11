using Choco.Application.Abstractions.Messaging;
using Choco.Contract.Users;
using Choco.SharedKernel.Results;

namespace Choco.Application.Users.UpdateMyBio;

internal sealed class UpdateMyBioCommandHandler(MyProfileWriter writer)
    : ICommandHandler<UpdateMyBioCommand, UserProfileResponse>
{
    public Task<Result<UserProfileResponse>> Handle(UpdateMyBioCommand command, CancellationToken cancellationToken)
    {
        return writer.UpdateAsync(user =>
        {
            user.ChangeBio(command.Bio);
            return Task.FromResult(Result.Success());
        }, cancellationToken);
    }
}
