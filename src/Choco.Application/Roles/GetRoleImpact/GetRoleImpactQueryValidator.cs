using Choco.Contract.Roles;
using Choco.Domain.Roles;

using FluentValidation;

namespace Choco.Application.Roles.GetRoleImpact;

internal sealed class GetRoleImpactQueryValidator : AbstractValidator<GetRoleImpactQuery>
{
    public GetRoleImpactQueryValidator()
    {
        RuleFor(query => query.Id).NotEmpty();

        RuleFor(query => query.Permissions)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(permissions => permissions.All(permission => permission is not null && Permission.All.Contains(permission)))
            .WithErrorCode("UnknownPermission")
            .WithMessage("'{PropertyName}' must only contain permissions from the catalog.");
    }
}
