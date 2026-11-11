using Choco.Application.AuditLogs;
using Choco.Application.AuditLogs.GetAuditLogs;
using Choco.Contract.AuditLogs;

using FluentValidation.TestHelper;

namespace Choco.Application.UnitTests.AuditLogs.GetAuditLogs;

public sealed class GetAuditLogsQueryValidatorTests
{
    private static readonly DateTimeOffset From = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly GetAuditLogsQuery ValidQuery = new(
        null, null, null, null, null, null, null, null, GetAuditLogsQuery.DefaultLimit);

    private readonly GetAuditLogsQueryValidator _validator = new();

    [Fact]
    public void Validate_WithoutFilters_HasNoErrors()
    {
        TestValidationResult<GetAuditLogsQuery> result = _validator.TestValidate(ValidQuery);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEveryFilter_HasNoErrors()
    {
        GetAuditLogsQuery query = ValidQuery with
        {
            ActorId = new Guid("00000000-0000-0000-0000-000000000001"),
            Action = "PostDeleted",
            TargetType = "Post",
            TargetId = new Guid("00000000-0000-0000-0000-000000000002"),
            AffectedUserId = new Guid("00000000-0000-0000-0000-000000000003"),
            From = From,
            To = From.AddDays(1)
        };

        TestValidationResult<GetAuditLogsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(GetAuditLogsQuery.MaxLimit)]
    public void Validate_WithLimitAtBound_HasNoErrors(int limit)
    {
        GetAuditLogsQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetAuditLogsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(GetAuditLogsQuery.MaxLimit + 1)]
    public void Validate_WithLimitOutOfRange_HasLimitError(int limit)
    {
        GetAuditLogsQuery query = ValidQuery with { Limit = limit };

        TestValidationResult<GetAuditLogsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Limit).Only();
    }

    [Fact]
    public void Validate_WithEmptyActorId_HasActorIdError()
    {
        GetAuditLogsQuery query = ValidQuery with { ActorId = Guid.Empty };

        TestValidationResult<GetAuditLogsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.ActorId).Only();
    }

    [Fact]
    public void Validate_WithEmptyTargetId_HasTargetIdError()
    {
        GetAuditLogsQuery query = ValidQuery with { TargetId = Guid.Empty };

        TestValidationResult<GetAuditLogsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.TargetId).Only();
    }

    [Fact]
    public void Validate_WithEmptyAffectedUserId_HasAffectedUserIdError()
    {
        GetAuditLogsQuery query = ValidQuery with { AffectedUserId = Guid.Empty };

        TestValidationResult<GetAuditLogsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.AffectedUserId).Only();
    }

    [Theory]
    [InlineData("")]
    [InlineData("postdeleted")]
    [InlineData("Unknown")]
    public void Validate_WithUnknownAction_HasActionError(string action)
    {
        GetAuditLogsQuery query = ValidQuery with { Action = action };

        TestValidationResult<GetAuditLogsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Action).Only();
    }

    [Theory]
    [InlineData("")]
    [InlineData("post")]
    [InlineData("Unknown")]
    public void Validate_WithUnknownTargetType_HasTargetTypeError(string targetType)
    {
        GetAuditLogsQuery query = ValidQuery with { TargetType = targetType };

        TestValidationResult<GetAuditLogsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.TargetType).Only();
    }

    [Fact]
    public void Validate_WithToLaterThanFrom_HasNoErrors()
    {
        GetAuditLogsQuery query = ValidQuery with { From = From, To = From.AddTicks(1) };

        TestValidationResult<GetAuditLogsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithToNotLaterThanFrom_HasToError(int offsetTicks)
    {
        GetAuditLogsQuery query = ValidQuery with { From = From, To = From.AddTicks(offsetTicks) };

        TestValidationResult<GetAuditLogsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.To).Only();
    }

    [Fact]
    public void Validate_WithValidCursor_HasNoErrors()
    {
        string cursor = new AuditLogCursor(From, new Guid("3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e6f")).Encode();
        GetAuditLogsQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetAuditLogsQuery> result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a cursor")]
    public void Validate_WithInvalidCursor_HasCursorError(string cursor)
    {
        GetAuditLogsQuery query = ValidQuery with { Cursor = cursor };

        TestValidationResult<GetAuditLogsQuery> result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(q => q.Cursor).Only();
    }
}
