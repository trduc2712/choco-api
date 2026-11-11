using System.Net.Http.Headers;
using System.Net.Http.Json;

using Choco.Domain.AuditLogs;
using Choco.Domain.Posts;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Audit;

public abstract class AuditTestsBase : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    protected AuditTestsBase(ChocoApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    protected ChocoApiFactory Factory { get; }

    protected HttpClient Client { get; }

    public async ValueTask InitializeAsync()
    {
        await Factory.ResetAsync();
    }

    public ValueTask DisposeAsync()
    {
        Client.Dispose();

        return ValueTask.CompletedTask;
    }

    protected static HttpRequestMessage Request(
        HttpMethod method, string path, object? body = null, string identityId = "identity-id")
    {
        HttpRequestMessage request = new(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: identityId));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    protected static void ShouldBeEntry(
        AuditLogEntry entry,
        Guid actorId,
        AuditAction action,
        AuditTargetType targetType,
        Guid targetId,
        Guid? affectedUserId = null,
        string? detail = null)
    {
        entry.ActorType.Should().Be(AuditActorType.User);
        entry.ActorId.Should().Be(actorId);
        entry.OccurredAt.Should().Be(ChocoApiFactory.StartTime);
        entry.Action.Should().Be(action);
        entry.TargetType.Should().Be(targetType);
        entry.TargetId.Should().Be(targetId);
        entry.AffectedUserId.Should().Be(affectedUserId);
        entry.Detail.Should().Be(detail);
    }

    protected async Task<User> AddUserAsync(string identityId, string username, Guid? roleId = null)
    {
        User user = User.Create(identityId, username, "Display Name", null);

        if (roleId is not null)
        {
            user.AssignRole(roleId.Value, null, ChocoApiFactory.StartTime);
        }

        await Factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(user);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return user;
    }

    protected async Task<Post> AddPostAsync(User author)
    {
        Post post = Post.Create("content", author.Id);

        await Factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(post);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return post;
    }

    protected Task<Guid> GetAdministratorRoleIdAsync()
    {
        return Factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .Where(role => role.SystemKey == SystemRoles.Admin)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    protected Task<List<AuditLogEntry>> ReadEntriesAsync()
    {
        return Factory.ExecuteDbContextAsync(dbContext => dbContext.Set<AuditLogEntry>()
            .AsNoTracking()
            .OrderBy(entry => entry.OccurredAt)
            .ThenBy(entry => entry.Id)
            .ToListAsync(TestContext.Current.CancellationToken));
    }
}
