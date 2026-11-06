using Choco.Contract.Users;

using FluentValidation;

namespace Choco.Application.Users.GetUserProfile;

internal sealed class GetUserProfileQueryValidator : AbstractValidator<GetUserProfileQuery>
{
    public GetUserProfileQueryValidator()
    {
        RuleFor(query => query.Id).NotEmpty();
    }
}
