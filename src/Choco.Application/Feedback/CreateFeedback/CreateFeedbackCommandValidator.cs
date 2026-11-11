using Choco.Contract.Feedback;
using Choco.Domain.FeedbackEntries;

using FluentValidation;

namespace Choco.Application.Feedback.CreateFeedback;

internal sealed class CreateFeedbackCommandValidator : AbstractValidator<CreateFeedbackCommand>
{
    public CreateFeedbackCommandValidator()
    {
        RuleFor(command => command.Category)
            .Must(category => FeedbackWire.TryParse<FeedbackCategory>(category, out _))
            .WithMessage($"'{{PropertyName}}' must be one of: {FeedbackWire.Options<FeedbackCategory>()}.");

        RuleFor(command => command.Message)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(message => message.Trim().Length >= FeedbackEntry.MessageMinLength)
            .WithErrorCode("TooShort")
            .WithMessage($"'{{PropertyName}}' must be at least {FeedbackEntry.MessageMinLength} characters after trimming.")
            .Must(message => message.Trim().Length <= FeedbackEntry.MessageMaxLength)
            .WithErrorCode("TooLong")
            .WithMessage($"'{{PropertyName}}' must be {FeedbackEntry.MessageMaxLength} characters or fewer after trimming.");

        RuleFor(command => command.Context).NotNull();

        When(command => command.Context is not null, () =>
        {
            RuleFor(command => command.Context.Path)
                .NotEmpty()
                .MaximumLength(FeedbackContext.PathMaxLength)
                .Must(path => path is null || path.StartsWith('/') && path.IndexOfAny(['?', '#']) < 0)
                .WithMessage("'{PropertyName}' must be a pathname that starts with '/' and has no query string or hash.");

            RuleFor(command => command.Context.Locale)
                .Must(locale => FeedbackWire.TryParse<FeedbackLocale>(locale, out _))
                .WithMessage($"'{{PropertyName}}' must be one of: {FeedbackWire.Options<FeedbackLocale>()}.");

            RuleFor(command => command.Context.Theme)
                .Must(theme => FeedbackWire.TryParse<FeedbackTheme>(theme, out _))
                .WithMessage($"'{{PropertyName}}' must be one of: {FeedbackWire.Options<FeedbackTheme>()}.");

            RuleFor(command => command.Context.UserAgent)
                .NotNull()
                .MaximumLength(FeedbackContext.UserAgentMaxLength);

            RuleFor(command => command.Context.AppVersion)
                .NotNull()
                .MaximumLength(FeedbackContext.AppVersionMaxLength);

            RuleFor(command => command.Context.Viewport).NotNull();

            When(command => command.Context.Viewport is not null, () =>
            {
                RuleFor(command => command.Context.Viewport.Width)
                    .InclusiveBetween(1, FeedbackViewportDto.MaxSize);

                RuleFor(command => command.Context.Viewport.Height)
                    .InclusiveBetween(1, FeedbackViewportDto.MaxSize);
            });
        });
    }
}
