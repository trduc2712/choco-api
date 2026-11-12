using Choco.Application.Content;
using Choco.Contract.Posts;

using FluentValidation;

namespace Choco.Application.Posts.GetAdminPosts;

internal sealed class GetAdminPostsQueryValidator : AbstractValidator<GetAdminPostsQuery>
{
    public GetAdminPostsQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize).InclusiveBetween(1, GetAdminPostsQuery.MaxPageSize);

        RuleFor(query => query.State)
            .Must(state => ContentStates.TryParse(state, out _))
            .When(query => query.State is not null)
            .WithMessage($"'{{PropertyName}}' must be one of: {ContentStates.Options}.");

        RuleFor(query => query.AuthorId)
            .NotEqual(Guid.Empty)
            .When(query => query.AuthorId is not null);

        RuleFor(query => query.To)
            .GreaterThan(query => query.From!.Value)
            .When(query => query.From is not null && query.To is not null)
            .WithMessage("'{PropertyName}' must be later than 'From'.");

        RuleFor(query => query.Q)
            .Must(text => text!.Trim().Length <= GetAdminPostsQuery.SearchMaxLength)
            .When(query => query.Q is not null)
            .WithErrorCode("TooLong")
            .WithMessage($"'{{PropertyName}}' must be {GetAdminPostsQuery.SearchMaxLength} characters or fewer after trimming.");
    }
}
