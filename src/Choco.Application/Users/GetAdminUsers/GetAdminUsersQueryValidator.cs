using Choco.Contract.Users;

using FluentValidation;

namespace Choco.Application.Users.GetAdminUsers;

internal sealed class GetAdminUsersQueryValidator : AbstractValidator<GetAdminUsersQuery>
{
    public GetAdminUsersQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize).InclusiveBetween(1, GetAdminUsersQuery.MaxPageSize);

        RuleFor(query => query.State)
            .Must(state => UserStates.TryParse(state, out _))
            .When(query => query.State is not null)
            .WithMessage($"'{{PropertyName}}' must be one of: {UserStates.Options}.");

        RuleFor(query => query.RoleId)
            .Must(roleId => roleId != Guid.Empty)
            .When(query => query.RoleId is not null)
            .WithMessage("'{PropertyName}' must not be empty.");

        RuleFor(query => query.Q)
            .Must(text => text!.Trim().Length <= GetAdminUsersQuery.SearchMaxLength)
            .When(query => query.Q is not null)
            .WithErrorCode("TooLong")
            .WithMessage($"'{{PropertyName}}' must be {GetAdminUsersQuery.SearchMaxLength} characters or fewer after trimming.");
    }
}
