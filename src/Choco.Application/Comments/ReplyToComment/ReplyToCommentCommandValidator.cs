using Choco.Contract.Comments;
using Choco.Domain.Comments;

using FluentValidation;

namespace Choco.Application.Comments.ReplyToComment;

internal sealed class ReplyToCommentCommandValidator : AbstractValidator<ReplyToCommentCommand>
{
    public ReplyToCommentCommandValidator()
    {
        RuleFor(command => command.CommentId).NotEmpty();

        RuleFor(command => command.Content)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(content => content.Trim().Length <= Comment.ContentMaxLength)
            .WithErrorCode("TooLong")
            .WithMessage($"'{{PropertyName}}' must be {Comment.ContentMaxLength} characters or fewer once leading and trailing whitespace is removed.");
    }
}
