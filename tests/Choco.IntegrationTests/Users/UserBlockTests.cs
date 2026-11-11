using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Comments;
using Choco.Domain.PostLikes;
using Choco.Domain.Posts;
using Choco.Domain.UserBlocks;
using Choco.Domain.UserFollows;
using Choco.Domain.Users;
using Choco.IntegrationTests.Notifications;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Users;

public sealed class UserBlockTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownUserId = new("00000000-0000-0000-0000-000000000001");
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public UserBlockTests(ChocoApiFactory factory)
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
    public async Task Blocking_CreatesBlockAndRemovesFollowsInBothDirections()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User target = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        User other = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        await UserBlockTestData.AddFollowAsync(_factory, caller, target);
        await UserBlockTestData.AddFollowAsync(_factory, target, caller);
        await UserBlockTestData.AddFollowAsync(_factory, caller, other);
        using HttpRequestMessage request = Request("PUT", target.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        UserBlock block = (await UserBlockTestData.GetBlocksAsync(_factory)).Should().ContainSingle().Subject;
        block.BlockerId.Should().Be(caller.Id);
        block.BlockedUserId.Should().Be(target.Id);
        block.BlockedAt.Should().Be(_factory.TimeProvider.GetUtcNow());
        UserFollow follow = (await UserBlockTestData.GetFollowsAsync(_factory)).Should().ContainSingle().Subject;
        follow.FollowedUserId.Should().Be(other.Id);
    }

    [Fact]
    public async Task RepeatedBlockRequests_KeepOneBlock()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User target = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        using HttpRequestMessage firstRequest = Request("PUT", target.Id);
        using HttpRequestMessage secondRequest = Request("PUT", target.Id);

        using HttpResponseMessage firstResponse = await _client.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        using HttpResponseMessage secondResponse = await _client.SendAsync(secondRequest, TestContext.Current.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UserBlockTestData.GetBlocksAsync(_factory)).Should().ContainSingle()
            .Which.BlockedAt.Should().Be(ChocoApiFactory.StartTime);
    }

    [Fact]
    public async Task ConcurrentBlockRequests_CreateOneBlock()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User target = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
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
        (await UserBlockTestData.GetBlocksAsync(_factory)).Should().ContainSingle();
    }

    [Fact]
    public async Task RepeatedUnblockRequests_RemoveOnlyTheRequestedBlock()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User target = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        User other = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        await UserBlockTestData.AddBlockAsync(_factory, caller, target);
        await UserBlockTestData.AddBlockAsync(_factory, caller, other);
        using HttpRequestMessage firstRequest = Request("DELETE", target.Id);
        using HttpRequestMessage secondRequest = Request("DELETE", target.Id);

        using HttpResponseMessage firstResponse = await _client.SendAsync(firstRequest, TestContext.Current.CancellationToken);
        using HttpResponseMessage secondResponse = await _client.SendAsync(secondRequest, TestContext.Current.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UserBlockTestData.GetBlocksAsync(_factory)).Should().ContainSingle().Which.BlockedUserId.Should().Be(other.Id);
    }

    [Fact]
    public async Task UnblockingTheCallerWhoWasBlockedByTarget_KeepsTheTargetsBlock()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User target = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        await UserBlockTestData.AddBlockAsync(_factory, target, caller);
        using HttpRequestMessage request = Request("DELETE", target.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UserBlockTestData.GetBlocksAsync(_factory)).Should().ContainSingle().Which.BlockerId.Should().Be(target.Id);
    }

    [Fact]
    public async Task BlockingYourself_ReturnsBadRequest()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = Request("PUT", caller.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "UserBlock.SelfBlock");
        (await UserBlockTestData.GetBlocksAsync(_factory)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task UnknownTarget_ReturnsNotFound(string method)
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = Request(method, UnknownUserId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "User.NotFound");
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task DeletedTarget_ReturnsNotFound(string method)
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User target = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        await NotificationTestData.DeleteUserAsync(_factory, target);
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
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be("userId");
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task NoToken_ReturnsUnauthorized(string method)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), $"/api/v1/users/{UnknownUserId}/block");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Following_WhenABlockExistsInEitherDirection_ReturnsForbidden(bool callerBlocked)
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User target = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        await (callerBlocked
            ? UserBlockTestData.AddBlockAsync(_factory, caller, target)
            : UserBlockTestData.AddBlockAsync(_factory, target, caller));
        using HttpRequestMessage request = Request("PUT", target.Id, "follow");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "UserBlock.Blocked");
        (await UserBlockTestData.GetFollowsAsync(_factory)).Should().BeEmpty();
        (await NotificationTestData.GetNotificationsAsync(_factory, target.Id)).Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Liking_WhenABlockExistsInEitherDirection_ReturnsForbidden(bool callerBlocked)
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Post post = await NotificationTestData.AddPostAsync(_factory, author);
        await (callerBlocked
            ? UserBlockTestData.AddBlockAsync(_factory, caller, author)
            : UserBlockTestData.AddBlockAsync(_factory, author, caller));
        using HttpRequestMessage request = Request("PUT", post.Id, "like", "posts");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "UserBlock.Blocked");
        (await _factory.ExecuteDbContextAsync(db => db.Set<PostLike>().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken))).Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Commenting_WhenABlockExistsInEitherDirection_ReturnsForbidden(bool callerBlocked)
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Post post = await NotificationTestData.AddPostAsync(_factory, author);
        await (callerBlocked
            ? UserBlockTestData.AddBlockAsync(_factory, caller, author)
            : UserBlockTestData.AddBlockAsync(_factory, author, caller));
        using HttpRequestMessage request = Request("POST", post.Id, "comments", "posts");
        request.Content = JsonContent.Create(new { content = "comment content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "UserBlock.Blocked");
        (await _factory.ExecuteDbContextAsync(db => db.Set<Comment>().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken))).Should().BeEmpty();
    }

    [Fact]
    public async Task AfterUnblocking_FollowingWorksAgain()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User target = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        await UserBlockTestData.AddBlockAsync(_factory, caller, target);
        using HttpRequestMessage unblockRequest = Request("DELETE", target.Id);
        using HttpRequestMessage followRequest = Request("PUT", target.Id, "follow");
        await _client.SendAsync(unblockRequest, TestContext.Current.CancellationToken);

        using HttpResponseMessage response = await _client.SendAsync(followRequest, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UserBlockTestData.GetFollowsAsync(_factory)).Should().ContainSingle();
    }

    private static HttpRequestMessage Request(string method, Guid id, string action = "block", string resource = "users")
    {
        return NotificationTestData.CreateRequest(new HttpMethod(method), $"/api/v1/{resource}/{id}/{action}");
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
