using Choco.Contract.Users;

using FluentValidation;

namespace Choco.Application.Users.GetMyBlocks;

internal sealed class GetMyBlocksQueryValidator : AbstractValidator<GetMyBlocksQuery>
{
    public GetMyBlocksQueryValidator()
    {
        RuleFor(query => query.Limit).InclusiveBetween(1, GetMyBlocksQuery.MaxLimit);

        RuleFor(query => query.Cursor)
            .Must(cursor => BlockCursor.TryDecode(cursor, out _))
            .When(query => query.Cursor is not null)
            .WithMessage("'{PropertyName}' is not a valid cursor.");
    }
}
