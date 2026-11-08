using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.UserFollows;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Users;

public sealed class UserFollowTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownUserId = new("00000000-0000-0000-0000-000000000001");
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public UserFollowTests(ChocoApiFactory factory)
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
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ConcurrentFollowRequests_CreateOneRelationship()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User target = await AddUserAsync("identity-id-2", "username-2");
        using HttpRequestMessage firstRequest = Request("PUT", target.Id);
        using HttpRequestMessage secondRequest = Request("PUT", target.Id);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Task<HttpResponseMessage> first = _client.SendAsync(firstRequest, cancellationToken);
        Task<HttpResponseMessage> second = _client.SendAsync(secondRequest, cancellationToken);
        await Task.WhenAll(first, second);

        using HttpResponseMessage firstResponse = await first;
        using HttpResponseMessage secondResponse = await second;
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        UserFollow follow = (await GetFollowsAsync(caller.Id)).Should().ContainSingle().Subject;
        follow.FollowedUserId.Should().Be(target.Id);
        follow.FollowedAt.Should().Be(_factory.TimeProvider.GetUtcNow());
        _factory.IdentityProvider.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task RepeatedUnfollowRequests_RemoveOnlyTheRequestedRelationship()
    {
        User caller = await AddUserAsync("identity-id", "username");
        User target = await AddUserAsync("identity-id-2", "username-2");
        User other = await AddUserAsync("identity-id-3", "username-3");
        await _factory.ExecuteDbContextAsync(db =>
        {
            DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
            db.AddRange(UserFollow.Create(caller.Id, target.Id, now).Value, UserFollow.Create(caller.Id, other.Id, now).Value);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage firstRequest = Request("DELETE", target.Id);
        using HttpRequestMessage secondRequest = Request("DELETE", target.Id);

        using HttpResponseMessage firstResponse = await _client.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        using HttpResponseMessage secondResponse = await _client.SendAsync(secondRequest, TestContext.Current.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetFollowsAsync(caller.Id)).Should().ContainSingle().Which.FollowedUserId.Should().Be(other.Id);
        _factory.IdentityProvider.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task FollowingYourself_ReturnsBadRequest()
    {
        User caller = await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request("PUT", caller.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "UserFollow.SelfFollow");
        (await GetFollowsAsync(caller.Id)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task UnknownTarget_ReturnsNotFound(string method)
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(method, UnknownUserId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "User.NotFound");
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task DeletedTarget_ReturnsNotFound(string method)
    {
        await AddUserAsync("identity-id", "username");
        User target = await AddUserAsync("identity-id-2", "username-2");
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Remove(target);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        using HttpRequestMessage request = Request(method, target.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "User.NotFound");
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task EmptyTargetId_ReturnsValidationProblem(string method)
    {
        using HttpRequestMessage request = Request(method, Guid.Empty);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Status.Should().Be(400);
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be("UserId");
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Validation.Failed");
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task NoToken_ReturnsUnauthorized(string method)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), $"/api/v1/users/{UnknownUserId}/follow");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<User> AddUserAsync(string identityId, string username)
    {
        User user = User.Create(identityId, username, null, null);
        await _factory.ExecuteDbContextAsync(db =>
        {
            db.Add(user);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return user;
    }

    private Task<List<UserFollow>> GetFollowsAsync(Guid followerId)
    {
        return _factory.ExecuteDbContextAsync(db => db.Set<UserFollow>().AsNoTracking()
            .Where(follow => follow.FollowerId == followerId)
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    private static HttpRequestMessage Request(string method, Guid userId)
    {
        HttpRequestMessage request = new(new HttpMethod(method), $"/api/v1/users/{userId}/follow");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));
        return request;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Status.Should().Be((int)status);
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be(code);
    }
}
