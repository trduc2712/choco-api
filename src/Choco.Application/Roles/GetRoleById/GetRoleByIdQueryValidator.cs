using Choco.Contract.Roles;

using FluentValidation;

namespace Choco.Application.Roles.GetRoleById;

internal sealed class GetRoleByIdQueryValidator : AbstractValidator<GetRoleByIdQuery>
{
    public GetRoleByIdQueryValidator()
    {
        RuleFor(query => query.Id).NotEmpty();
    }
}
