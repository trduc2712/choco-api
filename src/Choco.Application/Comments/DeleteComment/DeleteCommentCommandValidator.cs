using Choco.Contract.Comments;

using FluentValidation;

namespace Choco.Application.Comments.DeleteComment;

internal sealed class DeleteCommentCommandValidator : AbstractValidator<DeleteCommentCommand>
{
    public DeleteCommentCommandValidator()
    {
        RuleFor(command => command.CommentId).NotEmpty();
    }
}
