using Choco.Contract.Comments;
using Choco.Domain.Comments;

using FluentValidation;

namespace Choco.Application.Comments.CreateComment;

internal sealed class CreateCommentCommandValidator : AbstractValidator<CreateCommentCommand>
{
    public CreateCommentCommandValidator()
    {
        RuleFor(command => command.PostId).NotEmpty();

        RuleFor(command => command.Content)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(content => content.Trim().Length <= Comment.ContentMaxLength)
            .WithErrorCode("TooLong")
            .WithMessage($"'{{PropertyName}}' must be {Comment.ContentMaxLength} characters or fewer once leading and trailing whitespace is removed.");
    }
}
