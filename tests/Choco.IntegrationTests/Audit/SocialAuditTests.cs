using System.Net;
using System.Net.Http.Json;

using Choco.Contract.Feedback;
using Choco.Domain.AuditLogs;
using Choco.Domain.Posts;
using Choco.Domain.Users;

namespace Choco.IntegrationTests.Audit;

public sealed class SocialAuditTests(ChocoApiFactory factory) : AuditTestsBase(factory)
{
    [Fact]
    public async Task LikePost_RecordsPostLikedWithTheAuthorAsAffectedUser()
    {
        User author = await AddUserAsync("identity-id-2", "username-2");
        User liker = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        using HttpRequestMessage request = Request(HttpMethod.Put, $"/api/v1/posts/{post.Id}/like");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.Should().BeTrue();
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, liker.Id, AuditAction.PostLiked, AuditTargetType.Post, post.Id, author.Id);
    }

    [Fact]
    public async Task LikePost_WhenThePostDoesNotExist_RecordsNoEntry()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(HttpMethod.Put, $"/api/v1/posts/{Guid.NewGuid()}/like");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadEntriesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task UnlikePost_RecordsPostUnlikedWithTheAuthorAsAffectedUser()
    {
        User author = await AddUserAsync("identity-id-2", "username-2");
        User liker = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        using HttpRequestMessage request = Request(HttpMethod.Delete, $"/api/v1/posts/{post.Id}/like");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.Should().BeTrue();
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, liker.Id, AuditAction.PostUnliked, AuditTargetType.Post, post.Id, author.Id);
    }

    [Fact]
    public async Task UnlikePost_WhenThePostDoesNotExist_RecordsNoEntry()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(HttpMethod.Delete, $"/api/v1/posts/{Guid.NewGuid()}/like");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadEntriesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task FollowUser_RecordsUserFollowedWithTheFollowedUserAsAffectedUser()
    {
        User target = await AddUserAsync("identity-id-2", "username-2");
        User follower = await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(HttpMethod.Put, $"/api/v1/users/{target.Id}/follow");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.Should().BeTrue();
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, follower.Id, AuditAction.UserFollowed, AuditTargetType.User, target.Id, target.Id);
    }

    [Fact]
    public async Task FollowUser_WhenTheUserDoesNotExist_RecordsNoEntry()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(HttpMethod.Put, $"/api/v1/users/{Guid.NewGuid()}/follow");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadEntriesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task UnfollowUser_RecordsUserUnfollowedWithTheUnfollowedUserAsAffectedUser()
    {
        User target = await AddUserAsync("identity-id-2", "username-2");
        User follower = await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(HttpMethod.Delete, $"/api/v1/users/{target.Id}/follow");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.Should().BeTrue();
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, follower.Id, AuditAction.UserUnfollowed, AuditTargetType.User, target.Id, target.Id);
    }

    [Fact]
    public async Task UnfollowUser_WhenTheUserDoesNotExist_RecordsNoEntry()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(HttpMethod.Delete, $"/api/v1/users/{Guid.NewGuid()}/follow");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadEntriesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task SendFeedback_RecordsFeedbackSentWithTheFeedbackAsTarget()
    {
        User author = await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(HttpMethod.Post, "/api/v1/feedback", FeedbackBody("feedback message"));

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        CreatedFeedbackResponse created = (await response.Content.ReadFromJsonAsync<CreatedFeedbackResponse>(TestContext.Current.CancellationToken))!;
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        ShouldBeEntry(entry, author.Id, AuditAction.FeedbackSent, AuditTargetType.Feedback, created.Id);
        entry.Detail.Should().BeNull();
    }

    [Fact]
    public async Task SendFeedback_WhenTheMessageIsInvalid_RecordsNoEntry()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(HttpMethod.Post, "/api/v1/feedback", FeedbackBody("short"));

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadEntriesAsync()).Should().BeEmpty();
    }

    private static object FeedbackBody(string message)
    {
        return new
        {
            category = "suggestion",
            message,
            context = new
            {
                path = "/path",
                locale = "ja",
                theme = "dark",
                viewport = new { width = 1280, height = 720 },
                appVersion = "app-version"
            }
        };
    }
}
