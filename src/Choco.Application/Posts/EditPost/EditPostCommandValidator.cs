using Choco.Contract.Posts;
using Choco.Domain.Posts;

using FluentValidation;

namespace Choco.Application.Posts.EditPost;

internal sealed class EditPostCommandValidator : AbstractValidator<EditPostCommand>
{
    public EditPostCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();

        RuleFor(command => command.Content)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(content => content.Trim().Length <= Post.ContentMaxLength)
            .WithMessage($"'{{PropertyName}}' must be {Post.ContentMaxLength} characters or fewer once leading and trailing whitespace is removed.");
    }
}
