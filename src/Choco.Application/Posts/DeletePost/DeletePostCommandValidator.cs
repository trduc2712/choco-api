using Choco.Contract.Posts;

using FluentValidation;

namespace Choco.Application.Posts.DeletePost;

internal sealed class DeletePostCommandValidator : AbstractValidator<DeletePostCommand>
{
    public DeletePostCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}
