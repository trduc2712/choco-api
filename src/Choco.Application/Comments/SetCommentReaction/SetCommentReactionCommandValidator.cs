using Choco.Contract.Comments;

using FluentValidation;

namespace Choco.Application.Comments.SetCommentReaction;

internal sealed class SetCommentReactionCommandValidator : AbstractValidator<SetCommentReactionCommand>
{
    public SetCommentReactionCommandValidator()
    {
        RuleFor(command => command.CommentId).NotEmpty();

        RuleFor(command => command.Type)
            .Must(type => CommentReactionTypes.TryParse(type, out _))
            .WithMessage($"'{{PropertyName}}' must be one of: {CommentReactionTypes.Options}.");
    }
}
