using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.AuditLogs;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Webhooks;

public sealed class ClerkUserSyncTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly DateTimeOffset Synced = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public ClerkUserSyncTests(ChocoApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async ValueTask InitializeAsync()
    {
        await _factory.ResetAsync();
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task UserUpdated_ChangesUsernameDisplayNameAndAvatar()
    {
        await AddUserAsync("identity-id", "username", Synced);

        HttpStatusCode status = await SendAsync(UserUpdatedPayload(
            "identity-id", "new-username", "New", "Name", "https://example.com/new.png", Synced.AddMinutes(1)));

        status.Should().Be(HttpStatusCode.OK);
        User user = await GetUserAsync("identity-id");
        user.Username.Should().Be("new-username");
        user.DisplayName.Should().Be("New Name");
        user.AvatarUrl.Should().Be("https://example.com/new.png");
        user.IdentityUpdatedAt.Should().Be(Synced.AddMinutes(1));
    }

    [Fact]
    public async Task UserUpdated_RecordsProfileUpdatedBySystemWithTheNamesOfTheChangedFields()
    {
        User existing = await AddUserAsync("identity-id", "username", Synced, avatarUrl: "https://example.com/avatar.png");

        await SendAsync(UserUpdatedPayload("identity-id", "new-username", "first-name", "last-name", "https://example.com/avatar.png", Synced.AddMinutes(1)));

        AuditLogEntry entry = (await GetAuditEntriesAsync()).Should().ContainSingle().Subject;
        entry.ActorType.Should().Be(AuditActorType.System);
        entry.ActorId.Should().BeNull();
        entry.Action.Should().Be(AuditAction.ProfileUpdated);
        entry.TargetType.Should().Be(AuditTargetType.User);
        entry.TargetId.Should().Be(existing.Id);
        entry.AffectedUserId.Should().Be(existing.Id);
        entry.Changes.Select(change => change.Field).Should().BeEquivalentTo("username", "displayName");
    }

    [Fact]
    public async Task UserUpdatedForAUserNeverSynced_IsApplied()
    {
        await AddUserAsync("identity-id", "username", null);

        await SendAsync(UserUpdatedPayload("identity-id", "new-username", "first-name", "last-name", "https://example.com/avatar.png", Synced));

        (await GetUserAsync("identity-id")).Username.Should().Be("new-username");
    }

    [Fact]
    public async Task UserUpdatedWithAnEarlierTime_ChangesNothing()
    {
        await AddUserAsync("identity-id", "username", Synced);

        HttpStatusCode status = await SendAsync(UserUpdatedPayload(
            "identity-id", "older-username", "Older", "Name", "https://example.com/older.png", Synced.AddMinutes(-1)));

        status.Should().Be(HttpStatusCode.OK);
        User user = await GetUserAsync("identity-id");
        user.Username.Should().Be("username");
        user.IdentityUpdatedAt.Should().Be(Synced);
        (await GetAuditEntriesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task UserUpdatedDeliveredTwice_ChangesTheUserAndRecordsOnceOnly()
    {
        await AddUserAsync("identity-id", "username", Synced);
        string payload = UserUpdatedPayload("identity-id", "new-username", "first-name", "last-name", "https://example.com/avatar.png", Synced.AddMinutes(1));

        HttpStatusCode first = await SendAsync(payload);
        HttpStatusCode second = await SendAsync(payload);

        first.Should().Be(HttpStatusCode.OK);
        second.Should().Be(HttpStatusCode.OK);
        (await GetUserAsync("identity-id")).Username.Should().Be("new-username");
        (await GetAuditEntriesAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task UserUpdatedThatOnlyMovesTheClerkTime_AdvancesTheTimeAndRecordsNothing()
    {
        await AddUserAsync("identity-id", "username", Synced, displayName: "first-name last-name", avatarUrl: "https://example.com/avatar.png");

        await SendAsync(UserUpdatedPayload("identity-id", "username", "first-name", "last-name", "https://example.com/avatar.png", Synced.AddMinutes(5)));

        (await GetUserAsync("identity-id")).IdentityUpdatedAt.Should().Be(Synced.AddMinutes(5));
        (await GetAuditEntriesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task UserUpdatedForAnUnknownUser_IsAcknowledgedWithoutAUser()
    {
        HttpStatusCode status = await SendAsync(UserUpdatedPayload(
            "unknown-identity-id", "username", "first-name", "last-name", "https://example.com/avatar.png", Synced));

        status.Should().Be(HttpStatusCode.OK);
        (await GetUsersAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task UserUpdatedForADeletedUser_ChangesNothing()
    {
        await AddUserAsync("identity-id", "username", Synced);
        await SendAsync(UserDeletedPayload("identity-id"));

        HttpStatusCode status = await SendAsync(UserUpdatedPayload(
            "identity-id", "new-username", "first-name", "last-name", "https://example.com/avatar.png", Synced.AddMinutes(1)));

        status.Should().Be(HttpStatusCode.OK);
        User user = await GetUserAsync("identity-id");
        user.IsDeleted.Should().BeTrue();
        user.Username.Should().Be("username");
    }

    [Fact]
    public async Task UserUpdatedWithoutUsernameOrTime_ReturnsValidationProblem()
    {
        await AddUserAsync("identity-id", "username", Synced);

        using HttpRequestMessage withoutUsername = Sign(UserUpdatedPayload(
            "identity-id", null, "first-name", "last-name", null, Synced.AddMinutes(1)));
        using HttpRequestMessage withoutTime = Sign(JsonSerializer.Serialize(new
        {
            type = "user.updated",
            data = new { id = "identity-id", username = "new-username", first_name = "first-name", last_name = "last-name" }
        }));

        using HttpResponseMessage first = await _client.SendAsync(withoutUsername, TestContext.Current.CancellationToken);
        using HttpResponseMessage second = await _client.SendAsync(withoutTime, TestContext.Current.CancellationToken);

        first.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await second.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().BeEquivalentTo(["identityUpdatedAt"]);
        (await GetUserAsync("identity-id")).Username.Should().Be("username");
    }

    [Fact]
    public async Task RenamedUsername_CanBeTakenByAnotherUser()
    {
        await AddUserAsync("identity-id", "username", Synced);
        await SendAsync(UserUpdatedPayload("identity-id", "renamed", "first-name", "last-name", null, Synced.AddMinutes(1)));

        HttpStatusCode status = await SendAsync(UserCreatedPayload("identity-id-2", "username", Synced.AddMinutes(2)));

        status.Should().Be(HttpStatusCode.OK);
        (await GetUsersAsync()).Select(user => user.Username).Should().BeEquivalentTo("renamed", "username");
    }

    [Fact]
    public async Task UserUpdatedWithAUsernameAnotherUserHolds_ReturnsConflictThenSucceedsOnceItIsFree()
    {
        await AddUserAsync("identity-id", "username", Synced);
        await AddUserAsync("identity-id-2", "taken", Synced);
        string payload = UserUpdatedPayload("identity-id", "taken", "first-name", "last-name", null, Synced.AddMinutes(2));

        HttpStatusCode first = await SendAsync(payload);
        await SendAsync(UserUpdatedPayload("identity-id-2", "released", "first-name", "last-name", null, Synced.AddMinutes(1)));
        HttpStatusCode retry = await SendAsync(payload);

        first.Should().Be(HttpStatusCode.Conflict);
        retry.Should().Be(HttpStatusCode.OK);
        (await GetUserAsync("identity-id")).Username.Should().Be("taken");
        (await GetUserAsync("identity-id-2")).Username.Should().Be("released");
    }

    [Fact]
    public async Task UserDeleted_SoftDeletesTheUserAndRecordsAccountDeletedBySystem()
    {
        User existing = await AddUserAsync("identity-id", "username", Synced);

        HttpStatusCode status = await SendAsync(UserDeletedPayload("identity-id"));

        status.Should().Be(HttpStatusCode.OK);
        User user = await GetUserAsync("identity-id");
        user.IsDeleted.Should().BeTrue();
        user.DeletedAt.Should().Be(ChocoApiFactory.StartTime);
        AuditLogEntry entry = (await GetAuditEntriesAsync()).Should().ContainSingle().Subject;
        entry.ActorType.Should().Be(AuditActorType.System);
        entry.Action.Should().Be(AuditAction.AccountDeleted);
        entry.TargetId.Should().Be(existing.Id);
    }

    [Fact]
    public async Task UserDeleted_MakesLaterRequestsOfTheUserReturnUnauthorized()
    {
        await AddUserAsync("identity-id", "username", Synced);
        await SendAsync(UserDeletedPayload("identity-id"));
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/me/posts");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: "identity-id"));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("User.Deleted");
    }

    [Fact]
    public async Task UserDeleted_FreesTheUsernameForAnotherUser()
    {
        await AddUserAsync("identity-id", "username", Synced);
        await SendAsync(UserDeletedPayload("identity-id"));

        HttpStatusCode status = await SendAsync(UserCreatedPayload("identity-id-2", "username", Synced.AddMinutes(1)));

        status.Should().Be(HttpStatusCode.OK);
        (await GetUsersAsync()).Where(user => !user.IsDeleted).Should().ContainSingle().Which.IdentityId.Should().Be("identity-id-2");
    }

    [Fact]
    public async Task UserDeletedTwiceOrForAnUnknownUser_IsAcknowledgedAndRecordedOnce()
    {
        await AddUserAsync("identity-id", "username", Synced);

        HttpStatusCode first = await SendAsync(UserDeletedPayload("identity-id"));
        HttpStatusCode second = await SendAsync(UserDeletedPayload("identity-id"));
        HttpStatusCode unknown = await SendAsync(UserDeletedPayload("unknown-identity-id"));

        first.Should().Be(HttpStatusCode.OK);
        second.Should().Be(HttpStatusCode.OK);
        unknown.Should().Be(HttpStatusCode.OK);
        (await GetAuditEntriesAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task UserCreated_StoresTheClerkTimeSoAnEarlierUpdateIsIgnored()
    {
        await SendAsync(UserCreatedPayload("identity-id", "username", Synced));

        await SendAsync(UserUpdatedPayload("identity-id", "older-username", "first-name", "last-name", null, Synced.AddSeconds(-30)));

        User user = await GetUserAsync("identity-id");
        user.IdentityUpdatedAt.Should().Be(Synced);
        user.Username.Should().Be("username");
    }

    [Fact]
    public async Task FirstRequestOfANewUser_StoresTheClerkTimeFromTheClerkApi()
    {
        _factory.IdentityProvider.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = "identity-id",
                username = "username",
                first_name = "first-name",
                last_name = "last-name",
                image_url = "https://example.com/avatar.png",
                updated_at = Synced.ToUnixTimeMilliseconds()
            })
        });
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/me/posts");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: "identity-id"));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetUserAsync("identity-id")).IdentityUpdatedAt.Should().Be(Synced);
    }

    private async Task<HttpStatusCode> SendAsync(string payload)
    {
        using HttpRequestMessage request = Sign(payload);
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        return response.StatusCode;
    }

    private static HttpRequestMessage Sign(string payload)
    {
        return ClerkWebhookSigner.CreateSignedRequest(payload, Guid.NewGuid().ToString(), DateTimeOffset.UtcNow);
    }

    private async Task<User> AddUserAsync(
        string identityId,
        string username,
        DateTimeOffset? identityUpdatedAt,
        string? displayName = null,
        string? avatarUrl = null)
    {
        User user = User.Create(identityId, username, displayName, avatarUrl, identityUpdatedAt);

        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(user);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return user;
    }

    private Task<User> GetUserAsync(string identityId)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(user => user.IdentityId == identityId, TestContext.Current.CancellationToken));
    }

    private Task<List<User>> GetUsersAsync()
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    private Task<List<AuditLogEntry>> GetAuditEntriesAsync()
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<AuditLogEntry>()
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    private static string UserCreatedPayload(string identityId, string username, DateTimeOffset updatedAt)
    {
        return JsonSerializer.Serialize(new
        {
            type = "user.created",
            data = new
            {
                id = identityId,
                username,
                first_name = "first-name",
                last_name = "last-name",
                image_url = "https://example.com/avatar.png",
                updated_at = updatedAt.ToUnixTimeMilliseconds()
            }
        });
    }

    private static string UserUpdatedPayload(
        string identityId,
        string? username,
        string firstName,
        string lastName,
        string? imageUrl,
        DateTimeOffset updatedAt)
    {
        return JsonSerializer.Serialize(new
        {
            type = "user.updated",
            data = new
            {
                id = identityId,
                username,
                first_name = firstName,
                last_name = lastName,
                image_url = imageUrl,
                updated_at = updatedAt.ToUnixTimeMilliseconds()
            }
        });
    }

    private static string UserDeletedPayload(string identityId)
    {
        return JsonSerializer.Serialize(new
        {
            type = "user.deleted",
            data = new { deleted = true, id = identityId, @object = "user" }
        });
    }
}
