using Choco.Contract.Users;

using FluentValidation;

namespace Choco.Application.Users.GetAdminUser;

internal sealed class GetAdminUserQueryValidator : AbstractValidator<GetAdminUserQuery>
{
    public GetAdminUserQueryValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
    }
}
