using Choco.Application.Feedback.CreateFeedback;
using Choco.Contract.Feedback;
using Choco.Domain.FeedbackEntries;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Feedback.CreateFeedback;

public sealed class CreateFeedbackCommandValidatorTests
{
    private static readonly CreateFeedbackCommand ValidCommand = new(
        "bug",
        "feedback message",
        new FeedbackContextDto("/path", "en", "system", "user-agent", new FeedbackViewportDto(1280, 720), "app-version"));

    private readonly CreateFeedbackCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_AcceptsCommand()
    {
        var result = _validator.TestValidate(ValidCommand);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("bug")]
    [InlineData("suggestion")]
    [InlineData("other")]
    public void Validate_WithKnownCategory_AcceptsCategory(string category)
    {
        var result = _validator.TestValidate(ValidCommand with { Category = category });

        result.ShouldNotHaveValidationErrorFor(command => command.Category);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bug")]
    [InlineData("unknown")]
    public void Validate_WithUnknownCategory_RejectsCategory(string? category)
    {
        var result = _validator.TestValidate(ValidCommand with { Category = category! });

        result.ShouldHaveValidationErrorFor(command => command.Category).Only();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("         ")]
    public void Validate_WithBlankMessage_RejectsMessage(string? message)
    {
        var result = _validator.TestValidate(ValidCommand with { Message = message! });

        result.ShouldHaveValidationErrorFor(command => command.Message);
    }

    [Fact]
    public void Validate_WithMessageUnderLimitAfterTrimming_RejectsMessage()
    {
        var result = _validator.TestValidate(ValidCommand with
        {
            Message = $"  {new string('m', FeedbackEntry.MessageMinLength - 1)}  "
        });

        result.ShouldHaveValidationErrorFor(command => command.Message).Only();
    }

    [Fact]
    public void Validate_WithMessageOverLimit_RejectsMessage()
    {
        var result = _validator.TestValidate(ValidCommand with
        {
            Message = new string('m', FeedbackEntry.MessageMaxLength + 1)
        });

        result.ShouldHaveValidationErrorFor(command => command.Message).Only();
    }

    [Fact]
    public void Validate_WithMessageAtLimitsAfterTrimming_AcceptsMessage()
    {
        var atMinimum = _validator.TestValidate(ValidCommand with
        {
            Message = $"  {new string('m', FeedbackEntry.MessageMinLength)}  "
        });
        var atMaximum = _validator.TestValidate(ValidCommand with
        {
            Message = $"  {new string('m', FeedbackEntry.MessageMaxLength)}  "
        });

        atMinimum.ShouldNotHaveAnyValidationErrors();
        atMaximum.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithoutContext_RejectsContext()
    {
        var result = _validator.TestValidate(ValidCommand with { Context = null! });

        result.ShouldHaveValidationErrorFor(command => command.Context).Only();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("path")]
    [InlineData("/path?query=value")]
    [InlineData("/path#hash")]
    public void Validate_WithInvalidPath_RejectsPath(string? path)
    {
        var result = _validator.TestValidate(ValidCommand with { Context = ValidCommand.Context with { Path = path! } });

        result.ShouldHaveValidationErrorFor(command => command.Context.Path);
    }

    [Fact]
    public void Validate_WithPathOverLimit_RejectsPath()
    {
        var result = _validator.TestValidate(ValidCommand with
        {
            Context = ValidCommand.Context with { Path = "/" + new string('p', FeedbackContext.PathMaxLength) }
        });

        result.ShouldHaveValidationErrorFor(command => command.Context.Path).Only();
    }

    [Fact]
    public void Validate_WithPathAtLimit_AcceptsPath()
    {
        var result = _validator.TestValidate(ValidCommand with
        {
            Context = ValidCommand.Context with { Path = "/" + new string('p', FeedbackContext.PathMaxLength - 1) }
        });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("EN")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WithUnknownLocale_RejectsLocale(string? locale)
    {
        var result = _validator.TestValidate(ValidCommand with { Context = ValidCommand.Context with { Locale = locale! } });

        result.ShouldHaveValidationErrorFor(command => command.Context.Locale).Only();
    }

    [Theory]
    [InlineData("blue")]
    [InlineData("Dark")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WithUnknownTheme_RejectsTheme(string? theme)
    {
        var result = _validator.TestValidate(ValidCommand with { Context = ValidCommand.Context with { Theme = theme! } });

        result.ShouldHaveValidationErrorFor(command => command.Context.Theme).Only();
    }

    [Fact]
    public void Validate_WithUserAgentOverLimit_RejectsUserAgent()
    {
        var result = _validator.TestValidate(ValidCommand with
        {
            Context = ValidCommand.Context with { UserAgent = new string('u', FeedbackContext.UserAgentMaxLength + 1) }
        });

        result.ShouldHaveValidationErrorFor(command => command.Context.UserAgent).Only();
    }

    [Fact]
    public void Validate_WithUserAgentAtLimit_AcceptsUserAgent()
    {
        var result = _validator.TestValidate(ValidCommand with
        {
            Context = ValidCommand.Context with { UserAgent = new string('u', FeedbackContext.UserAgentMaxLength) }
        });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithAppVersionOverLimit_RejectsAppVersion()
    {
        var result = _validator.TestValidate(ValidCommand with
        {
            Context = ValidCommand.Context with { AppVersion = new string('a', FeedbackContext.AppVersionMaxLength + 1) }
        });

        result.ShouldHaveValidationErrorFor(command => command.Context.AppVersion).Only();
    }

    [Fact]
    public void Validate_WithAppVersionAtLimit_AcceptsAppVersion()
    {
        var result = _validator.TestValidate(ValidCommand with
        {
            Context = ValidCommand.Context with { AppVersion = new string('a', FeedbackContext.AppVersionMaxLength) }
        });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithoutViewport_RejectsViewport()
    {
        var result = _validator.TestValidate(ValidCommand with { Context = ValidCommand.Context with { Viewport = null! } });

        result.ShouldHaveValidationErrorFor(command => command.Context.Viewport).Only();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(FeedbackViewportDto.MaxSize + 1)]
    public void Validate_WithViewportSizeOutOfRange_RejectsWidthAndHeight(int size)
    {
        var result = _validator.TestValidate(ValidCommand with
        {
            Context = ValidCommand.Context with { Viewport = new FeedbackViewportDto(size, size) }
        });

        result.ShouldHaveValidationErrorFor(command => command.Context.Viewport.Width);
        result.ShouldHaveValidationErrorFor(command => command.Context.Viewport.Height);
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void Validate_WithViewportSizeAtLimits_AcceptsViewport()
    {
        var result = _validator.TestValidate(ValidCommand with
        {
            Context = ValidCommand.Context with { Viewport = new FeedbackViewportDto(1, FeedbackViewportDto.MaxSize) }
        });

        result.ShouldNotHaveAnyValidationErrors();
    }
}
