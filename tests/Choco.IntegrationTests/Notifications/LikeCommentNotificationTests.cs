using System.Net;
using System.Net.Http.Json;

using Choco.Domain.Comments;
using Choco.Domain.Notifications;
using Choco.Domain.Posts;
using Choco.Domain.Users;

namespace Choco.IntegrationTests.Notifications;

public sealed class LikeCommentNotificationTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public LikeCommentNotificationTests(ChocoApiFactory factory)
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
    public async Task Liking_NotifiesTheCommentAuthor()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User postAuthor = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        User commentAuthor = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        Post post = await NotificationTestData.AddPostAsync(_factory, postAuthor);
        Comment comment = await NotificationTestData.AddCommentAsync(_factory, post, commentAuthor);

        using HttpResponseMessage response = await ReactAsync(comment.Id, "like");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Notification notification = (await NotificationTestData.GetNotificationsAsync(_factory, commentAuthor.Id)).Should().ContainSingle().Subject;
        notification.ActorId.Should().Be(caller.Id);
        notification.Type.Should().Be(NotificationType.CommentLiked);
        notification.PostId.Should().Be(post.Id);
        notification.CommentId.Should().Be(comment.Id);
        notification.CreatedAt.Should().Be(_factory.TimeProvider.GetUtcNow());
        notification.ReadAt.Should().BeNull();
        (await NotificationTestData.GetNotificationsAsync(_factory, postAuthor.Id)).Should().BeEmpty();
        (await NotificationTestData.GetNotificationsAsync(_factory, caller.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Disliking_DoesNotNotify()
    {
        await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Comment comment = await NotificationTestData.AddCommentAsync(
            _factory, await NotificationTestData.AddPostAsync(_factory, author), author);

        using HttpResponseMessage response = await ReactAsync(comment.Id, "dislike");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await NotificationTestData.GetNotificationsAsync(_factory, author.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task LikingOwnComment_DoesNotNotify()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        Comment comment = await NotificationTestData.AddCommentAsync(
            _factory, await NotificationTestData.AddPostAsync(_factory, caller), caller);

        using HttpResponseMessage response = await ReactAsync(comment.Id, "like");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await NotificationTestData.GetNotificationsAsync(_factory, caller.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task ChangingADislikeToALike_NotifiesTheCommentAuthor()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Comment comment = await NotificationTestData.AddCommentAsync(
            _factory, await NotificationTestData.AddPostAsync(_factory, author), author);
        using HttpResponseMessage dislike = await ReactAsync(comment.Id, "dislike");

        using HttpResponseMessage like = await ReactAsync(comment.Id, "like");

        dislike.StatusCode.Should().Be(HttpStatusCode.NoContent);
        like.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Notification notification = (await NotificationTestData.GetNotificationsAsync(_factory, author.Id)).Should().ContainSingle().Subject;
        notification.ActorId.Should().Be(caller.Id);
        notification.Type.Should().Be(NotificationType.CommentLiked);
    }

    [Fact]
    public async Task LikingAgainAfterRemovingTheLike_DoesNotNotifyAgain()
    {
        await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Comment comment = await NotificationTestData.AddCommentAsync(
            _factory, await NotificationTestData.AddPostAsync(_factory, author), author);
        using HttpResponseMessage first = await ReactAsync(comment.Id, "like");
        using HttpRequestMessage removeRequest = NotificationTestData.CreateRequest(
            HttpMethod.Delete, $"/api/v1/comments/{comment.Id}/reaction", NotificationTestData.CallerIdentityId);
        using HttpResponseMessage removed = await _client.SendAsync(removeRequest, TestContext.Current.CancellationToken);

        using HttpResponseMessage second = await ReactAsync(comment.Id, "like");

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        removed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await NotificationTestData.GetNotificationsAsync(_factory, author.Id)).Should().ContainSingle();
    }

    [Fact]
    public async Task LikesFromDifferentUsers_NotifyTheAuthorOncePerUser()
    {
        await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        Comment comment = await NotificationTestData.AddCommentAsync(
            _factory, await NotificationTestData.AddPostAsync(_factory, author), author);

        using HttpResponseMessage first = await ReactAsync(comment.Id, "like");
        using HttpResponseMessage second = await ReactAsync(comment.Id, "like", "identity-id-3");

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await NotificationTestData.GetNotificationsAsync(_factory, author.Id)).Should().HaveCount(2);
    }

    private async Task<HttpResponseMessage> ReactAsync(
        Guid commentId, string type, string subject = NotificationTestData.CallerIdentityId)
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(
            HttpMethod.Put, $"/api/v1/comments/{commentId}/reaction", subject);
        request.Content = JsonContent.Create(new { type });

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
