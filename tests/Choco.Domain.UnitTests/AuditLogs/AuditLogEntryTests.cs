using System.Net;

using Choco.Domain.AuditLogs;

namespace Choco.Domain.UnitTests.AuditLogs;

public sealed class AuditLogEntryTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid ActorId = new("5f0c6b1e-2d4a-4c8e-9b7a-3e1f2a6d8c40");
    private static readonly Guid TargetId = new("7a3d9c2b-8e1f-4b6a-a5d4-1c0e9f8b7a21");
    private static readonly Guid AffectedUserId = new("2b8e4d6f-1a3c-4e9b-8f7d-6c5a4b3e2d10");
    private static readonly IPAddress IpAddress = IPAddress.Parse("192.0.2.1");

    [Fact]
    public void Create_WithValidData_ReturnsEntryWithThatData()
    {
        AuditLogEntry entry = AuditLogEntry.Create(
            OccurredAt,
            AuditActorType.User,
            ActorId,
            ["role-name-1", "role-name-2"],
            "https://client.example.com",
            AuditAction.PostDeleted,
            AuditTargetType.Post,
            TargetId,
            AffectedUserId,
            "detail",
            "trace-id",
            "user-agent",
            IpAddress);

        entry.Id.Should().NotBeEmpty();
        entry.OccurredAt.Should().Be(OccurredAt);
        entry.ActorType.Should().Be(AuditActorType.User);
        entry.ActorId.Should().Be(ActorId);
        entry.ActorRoles.Should().Equal("role-name-1", "role-name-2");
        entry.ClientId.Should().Be("https://client.example.com");
        entry.Action.Should().Be(AuditAction.PostDeleted);
        entry.TargetType.Should().Be(AuditTargetType.Post);
        entry.TargetId.Should().Be(TargetId);
        entry.AffectedUserId.Should().Be(AffectedUserId);
        entry.Detail.Should().Be("detail");
        entry.TraceId.Should().Be("trace-id");
        entry.UserAgent.Should().Be("user-agent");
        entry.IpAddress.Should().Be(IpAddress);
    }

    [Fact]
    public void Create_ForSystemActorWithoutOptionalData_ReturnsEntryWithoutThem()
    {
        AuditLogEntry entry = AuditLogEntry.Create(
            OccurredAt, AuditActorType.System, null, [], null, AuditAction.RoleAssigned, AuditTargetType.User, TargetId, null, null, null, null, null);

        entry.ActorType.Should().Be(AuditActorType.System);
        entry.ActorId.Should().BeNull();
        entry.ActorRoles.Should().BeEmpty();
        entry.ClientId.Should().BeNull();
        entry.AffectedUserId.Should().BeNull();
        entry.Detail.Should().BeNull();
        entry.TraceId.Should().BeNull();
        entry.UserAgent.Should().BeNull();
        entry.IpAddress.Should().BeNull();
    }

    [Fact]
    public void Create_WithActorRolesList_CopiesTheRoles()
    {
        List<string> roles = ["role-name"];

        AuditLogEntry entry = CreateEntry(actorRoles: roles);
        roles.Add("role-name-2");

        entry.ActorRoles.Should().Equal("role-name");
    }

    [Fact]
    public void Create_ForUserActorWithoutId_ThrowsArgumentException()
    {
        Func<AuditLogEntry> act = () => AuditLogEntry.Create(
            OccurredAt, AuditActorType.User, null, [], null, AuditAction.PostCreated, AuditTargetType.Post, TargetId, null, null, null, null, null);

        act.Should().Throw<ArgumentException>().WithParameterName("actorId");
    }

    [Fact]
    public void Create_ForUserActorWithEmptyId_ThrowsArgumentException()
    {
        Func<AuditLogEntry> act = () => AuditLogEntry.Create(
            OccurredAt, AuditActorType.User, Guid.Empty, [], null, AuditAction.PostCreated, AuditTargetType.Post, TargetId, null, null, null, null, null);

        act.Should().Throw<ArgumentException>().WithParameterName("actorId");
    }

    [Fact]
    public void Create_ForSystemActorWithId_ThrowsArgumentException()
    {
        Func<AuditLogEntry> act = () => AuditLogEntry.Create(
            OccurredAt, AuditActorType.System, ActorId, [], null, AuditAction.PostCreated, AuditTargetType.Post, TargetId, null, null, null, null, null);

        act.Should().Throw<ArgumentException>().WithParameterName("actorId");
    }

    [Fact]
    public void Create_WithEmptyTargetId_ThrowsArgumentException()
    {
        Func<AuditLogEntry> act = () => AuditLogEntry.Create(
            OccurredAt, AuditActorType.User, ActorId, [], null, AuditAction.PostCreated, AuditTargetType.Post, Guid.Empty, null, null, null, null, null);

        act.Should().Throw<ArgumentException>().WithParameterName("targetId");
    }

    [Fact]
    public void Create_WithDetailAtMaxLength_ReturnsEntryWithThatDetail()
    {
        string detail = new('d', AuditLogEntry.DetailMaxLength);

        AuditLogEntry entry = CreateEntry(detail: detail);

        entry.Detail.Should().Be(detail);
    }

    [Fact]
    public void Create_WithDetailOverMaxLength_ThrowsArgumentException()
    {
        string detail = new('d', AuditLogEntry.DetailMaxLength + 1);

        Func<AuditLogEntry> act = () => CreateEntry(detail: detail);

        act.Should().Throw<ArgumentException>().WithParameterName("detail");
    }

    [Fact]
    public void Create_WithTraceIdAtMaxLength_ReturnsEntryWithThatTraceId()
    {
        string traceId = new('t', AuditLogEntry.TraceIdMaxLength);

        AuditLogEntry entry = CreateEntry(traceId: traceId);

        entry.TraceId.Should().Be(traceId);
    }

    [Fact]
    public void Create_WithTraceIdOverMaxLength_ThrowsArgumentException()
    {
        string traceId = new('t', AuditLogEntry.TraceIdMaxLength + 1);

        Func<AuditLogEntry> act = () => CreateEntry(traceId: traceId);

        act.Should().Throw<ArgumentException>().WithParameterName("traceId");
    }

    [Fact]
    public void Create_WithUserAgentAtMaxLength_ReturnsEntryWithThatUserAgent()
    {
        string userAgent = new('u', AuditLogEntry.UserAgentMaxLength);

        AuditLogEntry entry = CreateEntry(userAgent: userAgent);

        entry.UserAgent.Should().Be(userAgent);
    }

    [Fact]
    public void Create_WithUserAgentOverMaxLength_CutsItToMaxLength()
    {
        string userAgent = new('u', AuditLogEntry.UserAgentMaxLength + 1);

        AuditLogEntry entry = CreateEntry(userAgent: userAgent);

        entry.UserAgent.Should().Be(new string('u', AuditLogEntry.UserAgentMaxLength));
    }

    [Fact]
    public void Create_WithClientIdOverMaxLength_CutsItToMaxLength()
    {
        string clientId = new('c', AuditLogEntry.ClientIdMaxLength + 1);

        AuditLogEntry entry = CreateEntry(clientId: clientId);

        entry.ClientId.Should().Be(new string('c', AuditLogEntry.ClientIdMaxLength));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_WithBlankUserAgentOrClientId_StoresNull(string value)
    {
        AuditLogEntry entry = CreateEntry(userAgent: value, clientId: value);

        entry.UserAgent.Should().BeNull();
        entry.ClientId.Should().BeNull();
    }

    private static AuditLogEntry CreateEntry(
        IEnumerable<string>? actorRoles = null,
        string? clientId = null,
        string? detail = null,
        string? traceId = null,
        string? userAgent = null)
    {
        return AuditLogEntry.Create(
            OccurredAt,
            AuditActorType.User,
            ActorId,
            actorRoles ?? [],
            clientId,
            AuditAction.PostCreated,
            AuditTargetType.Post,
            TargetId,
            null,
            detail,
            traceId,
            userAgent,
            null);
    }
}
