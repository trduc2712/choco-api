using Choco.Contract.Users;
using Choco.Domain.Users;

using FluentValidation;

namespace Choco.Application.Users.UpdateMyBio;

internal sealed class UpdateMyBioCommandValidator : AbstractValidator<UpdateMyBioCommand>
{
    public UpdateMyBioCommandValidator()
    {
        RuleFor(command => command.Bio)
            .Must(bio => bio is null || bio.Trim().Length <= User.BioMaxLength)
            .WithErrorCode("TooLong")
            .WithMessage($"'Bio' must be at most {User.BioMaxLength} characters.");
    }
}
