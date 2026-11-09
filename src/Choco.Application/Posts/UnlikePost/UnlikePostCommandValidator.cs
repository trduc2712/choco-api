using Choco.Contract.Posts;

using FluentValidation;

namespace Choco.Application.Posts.UnlikePost;

internal sealed class UnlikePostCommandValidator : AbstractValidator<UnlikePostCommand>
{
    public UnlikePostCommandValidator()
    {
        RuleFor(command => command.PostId).NotEmpty();
    }
}
