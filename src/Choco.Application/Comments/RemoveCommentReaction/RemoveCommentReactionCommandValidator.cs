using Choco.Contract.Comments;

using FluentValidation;

namespace Choco.Application.Comments.RemoveCommentReaction;

internal sealed class RemoveCommentReactionCommandValidator : AbstractValidator<RemoveCommentReactionCommand>
{
    public RemoveCommentReactionCommandValidator()
    {
        RuleFor(command => command.CommentId).NotEmpty();
    }
}
