using Choco.Contract.Posts;

using FluentValidation;

namespace Choco.Application.Posts.LikePost;

internal sealed class LikePostCommandValidator : AbstractValidator<LikePostCommand>
{
    public LikePostCommandValidator()
    {
        RuleFor(command => command.PostId).NotEmpty();
    }
}
