using Choco.Application.Notifications;
using Choco.Application.Notifications.GetMyNotifications;
using Choco.Contract.Notifications;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.Notifications.GetMyNotifications;

public sealed class GetMyNotificationsQueryValidatorTests
{
    private static readonly GetMyNotificationsQuery ValidQuery = new(null, GetMyNotificationsQuery.DefaultLimit);

    private readonly GetMyNotificationsQueryValidator _validator = new();

    [Fact]
    public void Validate_WithoutCursor_HasNoErrors()
    {
        TestValidationResult<GetMyNotificationsQuery> result = _validator.TestValidate(ValidQuery);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithValidCursor_HasNoErrors()
    {
        string cursor = new NotificationCursor(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new Guid("3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e6f")).Encode();
        GetMyNotificationsQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetMyNotificationsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(GetMyNotificationsQuery.MaxLimit)]
    public void Validate_WithLimitAtBound_HasNoErrors(int limit)
    {
        GetMyNotificationsQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetMyNotificationsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GetMyNotificationsQuery.MaxLimit + 1)]
    public void Validate_WithLimitOutOfRange_HasLimitError(int limit)
    {
        GetMyNotificationsQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetMyNotificationsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Limit).Only();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a cursor")]
    public void Validate_WithInvalidCursor_HasCursorError(string cursor)
    {
        GetMyNotificationsQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetMyNotificationsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Cursor).Only();
    }
}
