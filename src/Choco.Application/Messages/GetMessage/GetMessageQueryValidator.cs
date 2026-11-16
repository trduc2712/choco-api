using Choco.Contract.Messages;

using FluentValidation;

namespace Choco.Application.Messages.GetMessage;

internal sealed class GetMessageQueryValidator : AbstractValidator<GetMessageQuery>
{
    public GetMessageQueryValidator()
    {
        RuleFor(query => query.Id).NotEmpty();
    }
}
