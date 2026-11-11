using Choco.Contract.Posts;
using Choco.Domain.Posts;

using FluentValidation;

namespace Choco.Application.Posts.CreatePost;

internal sealed class CreatePostCommandValidator : AbstractValidator<CreatePostCommand>
{
    public CreatePostCommandValidator()
    {
        RuleFor(command => command.Content)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(content => content.Trim().Length <= Post.ContentMaxLength)
            .WithErrorCode("TooLong")
            .WithMessage($"'{{PropertyName}}' must be {Post.ContentMaxLength} characters or fewer once leading and trailing whitespace is removed.");
    }
}
