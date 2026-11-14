using Choco.Contract.Users;
using Choco.Domain.Users;

using FluentValidation;

namespace Choco.Application.Users.SyncUserProfile;

internal sealed class SyncUserProfileCommandValidator : AbstractValidator<SyncUserProfileCommand>
{
    public SyncUserProfileCommandValidator()
    {
        RuleFor(command => command.IdentityId).NotEmpty().MaximumLength(User.IdentityIdMaxLength);
        RuleFor(command => command.Username).NotEmpty().MaximumLength(User.UsernameMaxLength);
        RuleFor(command => command.DisplayName).MaximumLength(User.DisplayNameMaxLength);
        RuleFor(command => command.AvatarUrl).MaximumLength(User.AvatarUrlMaxLength);
        RuleFor(command => command.IdentityUpdatedAt).GreaterThan(DateTimeOffset.UnixEpoch);
    }
}
