using System.Net;

using Choco.Domain.Notifications;
using Choco.Domain.Posts;
using Choco.Domain.Users;

namespace Choco.IntegrationTests.Notifications;

public sealed class LikePostNotificationTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public LikePostNotificationTests(ChocoApiFactory factory)
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
    public async Task Liking_NotifiesThePostAuthor()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Post post = await NotificationTestData.AddPostAsync(_factory, author);

        using HttpResponseMessage response = await LikeAsync(post.Id);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Notification notification = (await NotificationTestData.GetNotificationsAsync(_factory, author.Id)).Should().ContainSingle().Subject;
        notification.ActorId.Should().Be(caller.Id);
        notification.Type.Should().Be(NotificationType.PostLiked);
        notification.PostId.Should().Be(post.Id);
        notification.CreatedAt.Should().Be(_factory.TimeProvider.GetUtcNow());
        notification.ReadAt.Should().BeNull();
        (await NotificationTestData.GetNotificationsAsync(_factory, caller.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task LikingOwnPost_DoesNotNotify()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        Post post = await NotificationTestData.AddPostAsync(_factory, caller);

        using HttpResponseMessage response = await LikeAsync(post.Id);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await NotificationTestData.GetNotificationsAsync(_factory, caller.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task RepeatedLikeRequests_CreateOneNotification()
    {
        await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Post post = await NotificationTestData.AddPostAsync(_factory, author);

        using HttpResponseMessage firstResponse = await LikeAsync(post.Id);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        using HttpResponseMessage secondResponse = await LikeAsync(post.Id);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await NotificationTestData.GetNotificationsAsync(_factory, author.Id)).Should().ContainSingle()
            .Which.CreatedAt.Should().Be(ChocoApiFactory.StartTime);
    }

    [Fact]
    public async Task LikingAgainAfterUnliking_DoesNotNotifyAgain()
    {
        await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Post post = await NotificationTestData.AddPostAsync(_factory, author);
        using HttpResponseMessage firstLike = await LikeAsync(post.Id);
        using HttpRequestMessage unlikeRequest = NotificationTestData.CreateRequest(HttpMethod.Delete, $"/api/v1/posts/{post.Id}/like");
        using HttpResponseMessage unlike = await _client.SendAsync(unlikeRequest, TestContext.Current.CancellationToken);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));

        using HttpResponseMessage response = await LikeAsync(post.Id);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await NotificationTestData.GetNotificationsAsync(_factory, author.Id)).Should().ContainSingle();
    }

    [Fact]
    public async Task LikesFromDifferentUsers_NotifyTheAuthorOncePerUser()
    {
        await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        Post post = await NotificationTestData.AddPostAsync(_factory, author);

        using HttpResponseMessage first = await LikeAsync(post.Id);
        using HttpResponseMessage second = await LikeAsync(post.Id, "identity-id-3");

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await NotificationTestData.GetNotificationsAsync(_factory, author.Id)).Should().HaveCount(2);
    }

    private async Task<HttpResponseMessage> LikeAsync(Guid postId, string subject = NotificationTestData.CallerIdentityId)
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(
            HttpMethod.Put, $"/api/v1/posts/{postId}/like", subject);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
