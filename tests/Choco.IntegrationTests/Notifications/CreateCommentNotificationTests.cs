using System.Net;
using System.Net.Http.Json;

using Choco.Domain.Comments;
using Choco.Domain.Notifications;
using Choco.Domain.Posts;
using Choco.Domain.Users;

using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Notifications;

public sealed class CreateCommentNotificationTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public CreateCommentNotificationTests(ChocoApiFactory factory)
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
    public async Task Commenting_NotifiesThePostAuthor()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Post post = await NotificationTestData.AddPostAsync(_factory, author);

        using HttpResponseMessage response = await CommentAsync(post.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        Notification notification = (await NotificationTestData.GetNotificationsAsync(_factory, author.Id)).Should().ContainSingle().Subject;
        notification.ActorId.Should().Be(caller.Id);
        notification.Type.Should().Be(NotificationType.PostCommented);
        notification.PostId.Should().Be(post.Id);
        notification.CommentId.Should().Be((await GetCommentsAsync()).Should().ContainSingle().Subject.Id);
        notification.CreatedAt.Should().Be(_factory.TimeProvider.GetUtcNow());
        notification.ReadAt.Should().BeNull();
        (await NotificationTestData.GetNotificationsAsync(_factory, caller.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task CommentingOnOwnPost_DoesNotNotify()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        Post post = await NotificationTestData.AddPostAsync(_factory, caller);

        using HttpResponseMessage response = await CommentAsync(post.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await NotificationTestData.GetNotificationsAsync(_factory, caller.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task EveryComment_NotifiesThePostAuthor()
    {
        await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Post post = await NotificationTestData.AddPostAsync(_factory, author);

        using HttpResponseMessage first = await CommentAsync(post.Id);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        using HttpResponseMessage second = await CommentAsync(post.Id);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        (await NotificationTestData.GetNotificationsAsync(_factory, author.Id)).Select(notification => notification.CreatedAt)
            .Should().Equal(ChocoApiFactory.StartTime, ChocoApiFactory.StartTime.AddHours(1));
    }

    [Fact]
    public async Task InvalidComment_DoesNotNotify()
    {
        await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User author = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Post post = await NotificationTestData.AddPostAsync(_factory, author);
        using HttpRequestMessage request = NotificationTestData.CreateRequest(
            HttpMethod.Post, $"/api/v1/posts/{post.Id}/comments");
        request.Content = JsonContent.Create(new { content = " " });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await NotificationTestData.GetNotificationsAsync(_factory, author.Id)).Should().BeEmpty();
    }

    private Task<List<Comment>> GetCommentsAsync()
    {
        return _factory.ExecuteDbContextAsync(db => db.Set<Comment>().AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    private async Task<HttpResponseMessage> CommentAsync(Guid postId)
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(
            HttpMethod.Post, $"/api/v1/posts/{postId}/comments");
        request.Content = JsonContent.Create(new { content = "content" });

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
