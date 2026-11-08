using Choco.Contract.Abstractions.Messaging;

namespace Choco.Contract.Users;

public sealed record UploadMyAvatarCommand(Stream Image) : ICommand<UserProfileResponse>
{
    public const int MaxImageBytes = 10_000_000;
}
