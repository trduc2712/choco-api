using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Comments;
using Choco.Domain.Notifications;
using Choco.Domain.Posts;
using Choco.Domain.Users;

using Microsoft.AspNetCore.Mvc;

namespace Choco.IntegrationTests.Notifications;

public sealed class GetMyNotificationsTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string Path = "/api/v1/me/notifications";

    private static readonly DateTimeOffset FirstAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondAt = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ThirdAt = new(2026, 1, 3, 0, 0, 0, TimeSpan.Zero);

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetMyNotificationsTests(ChocoApiFactory factory)
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
    public async Task NoToken_ReturnsUnauthorized()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, Path);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CallerWithoutNotifications_GetsEmptyPage()
    {
        await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");

        PageBody body = await GetPageAsync(Path);

        body.Items.Should().BeEmpty();
        body.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Notifications_AreListedNewestFirstWithTheActor()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User firstActor = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        User secondActor = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        Notification first = await NotificationTestData.AddNotificationAsync(_factory, caller, firstActor, FirstAt);
        Notification second = await NotificationTestData.AddNotificationAsync(_factory, caller, secondActor, SecondAt);
        await _factory.ExecuteDbContextAsync(db =>
        {
            Notification read = db.Set<Notification>().Single(notification => notification.Id == first.Id);
            read.MarkRead(ThirdAt);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        PageBody body = await GetPageAsync(Path);

        body.NextCursor.Should().BeNull();
        body.Items.Should().HaveCount(2);
        NotificationBody newest = body.Items[0];
        newest.Id.Should().Be(second.Id);
        newest.Type.Should().Be("Followed");
        newest.Actor.Id.Should().Be(secondActor.Id);
        newest.Actor.Username.Should().Be("username-3");
        newest.Actor.DisplayName.Should().Be("username-3");
        newest.Actor.AvatarUrl.Should().BeNull();
        newest.CreatedAt.Should().Be(SecondAt);
        newest.IsRead.Should().BeFalse();
        NotificationBody oldest = body.Items[1];
        oldest.Id.Should().Be(first.Id);
        oldest.Actor.Id.Should().Be(firstActor.Id);
        oldest.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task FollowedNotification_HasNoPostOrCommentContext()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User actor = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        await NotificationTestData.AddNotificationAsync(_factory, caller, actor, FirstAt);

        PageBody body = await GetPageAsync(Path);

        NotificationBody item = body.Items.Should().ContainSingle().Subject;
        item.PostId.Should().BeNull();
        item.PostExcerpt.Should().BeNull();
        item.CommentId.Should().BeNull();
        item.CommentExcerpt.Should().BeNull();
    }

    [Fact]
    public async Task PostLikedNotification_ListsThePostWithItsExcerpt()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User actor = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Post post = await NotificationTestData.AddPostAsync(_factory, caller, "post content");
        await NotificationTestData.AddNotificationAsync(_factory, caller, actor, FirstAt, NotificationType.PostLiked, post);

        PageBody body = await GetPageAsync(Path);

        NotificationBody item = body.Items.Should().ContainSingle().Subject;
        item.Type.Should().Be("PostLiked");
        item.PostId.Should().Be(post.Id);
        item.PostExcerpt.Should().Be("post content");
        item.CommentId.Should().BeNull();
        item.CommentExcerpt.Should().BeNull();
    }

    [Fact]
    public async Task PostCommentedNotification_ListsThePostAndTheCommentWithTheirExcerpts()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User actor = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Post post = await NotificationTestData.AddPostAsync(_factory, caller, "post content");
        Comment comment = await NotificationTestData.AddCommentAsync(_factory, post, actor, "comment content");
        await NotificationTestData.AddNotificationAsync(
            _factory, caller, actor, FirstAt, NotificationType.PostCommented, post, comment);

        PageBody body = await GetPageAsync(Path);

        NotificationBody item = body.Items.Should().ContainSingle().Subject;
        item.Type.Should().Be("PostCommented");
        item.PostId.Should().Be(post.Id);
        item.PostExcerpt.Should().Be("post content");
        item.CommentId.Should().Be(comment.Id);
        item.CommentExcerpt.Should().Be("comment content");
    }

    [Fact]
    public async Task LongPostAndComment_AreListedAsShortenedExcerpts()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User actor = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        string content = new('c', Post.ContentMaxLength);
        Post post = await NotificationTestData.AddPostAsync(_factory, caller, content);
        Comment comment = await NotificationTestData.AddCommentAsync(_factory, post, actor, content);
        await NotificationTestData.AddNotificationAsync(
            _factory, caller, actor, FirstAt, NotificationType.PostCommented, post, comment);

        PageBody body = await GetPageAsync(Path);

        NotificationBody item = body.Items.Should().ContainSingle().Subject;
        item.PostExcerpt.Should().EndWith("…").And.HaveLength(140).And.StartWith("ccc");
        item.CommentExcerpt.Should().EndWith("…").And.HaveLength(140).And.StartWith("ccc");
    }

    [Fact]
    public async Task DeletedPostAndComment_KeepTheirIdsAndHaveNoExcerpts()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User actor = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Post post = await NotificationTestData.AddPostAsync(_factory, caller);
        Comment comment = await NotificationTestData.AddCommentAsync(_factory, post, actor);
        await NotificationTestData.AddNotificationAsync(
            _factory, caller, actor, FirstAt, NotificationType.PostCommented, post, comment);
        await NotificationTestData.DeleteCommentAsync(_factory, comment);
        await NotificationTestData.DeletePostAsync(_factory, post);

        PageBody body = await GetPageAsync(Path);

        NotificationBody item = body.Items.Should().ContainSingle().Subject;
        item.PostId.Should().Be(post.Id);
        item.PostExcerpt.Should().BeNull();
        item.CommentId.Should().Be(comment.Id);
        item.CommentExcerpt.Should().BeNull();
    }

    [Fact]
    public async Task DeletedComment_OnALivePost_KeepsThePostExcerptOnly()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User actor = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Post post = await NotificationTestData.AddPostAsync(_factory, caller, "post content");
        Comment comment = await NotificationTestData.AddCommentAsync(_factory, post, actor);
        await NotificationTestData.AddNotificationAsync(
            _factory, caller, actor, FirstAt, NotificationType.PostCommented, post, comment);
        await NotificationTestData.DeleteCommentAsync(_factory, comment);

        PageBody body = await GetPageAsync(Path);

        NotificationBody item = body.Items.Should().ContainSingle().Subject;
        item.PostExcerpt.Should().Be("post content");
        item.CommentId.Should().Be(comment.Id);
        item.CommentExcerpt.Should().BeNull();
    }

    [Fact]
    public async Task Notifications_ArePagedByCursor()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User actor = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Notification first = await NotificationTestData.AddNotificationAsync(_factory, caller, actor, FirstAt);
        Notification second = await NotificationTestData.AddNotificationAsync(_factory, caller, actor, SecondAt);
        Notification third = await NotificationTestData.AddNotificationAsync(_factory, caller, actor, ThirdAt);

        PageBody firstPage = await GetPageAsync($"{Path}?limit=2");
        PageBody secondPage = await GetPageAsync($"{Path}?limit=2&cursor={Uri.EscapeDataString(firstPage.NextCursor!)}");

        firstPage.Items.Select(item => item.Id).Should().Equal(third.Id, second.Id);
        firstPage.NextCursor.Should().NotBeNullOrEmpty();
        secondPage.Items.Select(item => item.Id).Should().Equal(first.Id);
        secondPage.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task NotificationsOfOtherUsers_AreNotListed()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User other = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        User actor = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        Notification own = await NotificationTestData.AddNotificationAsync(_factory, caller, actor, FirstAt);
        await NotificationTestData.AddNotificationAsync(_factory, other, actor, SecondAt);

        PageBody body = await GetPageAsync(Path);

        body.Items.Should().ContainSingle().Which.Id.Should().Be(own.Id);
    }

    [Fact]
    public async Task NotificationOfDeletedActor_IsNotListed()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User deletedActor = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        User actor = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        await NotificationTestData.AddNotificationAsync(_factory, caller, deletedActor, FirstAt);
        Notification kept = await NotificationTestData.AddNotificationAsync(_factory, caller, actor, SecondAt);
        await NotificationTestData.DeleteUserAsync(_factory, deletedActor);

        PageBody body = await GetPageAsync(Path);

        body.Items.Should().ContainSingle().Which.Id.Should().Be(kept.Id);
    }

    [Theory]
    [InlineData("limit=0", "limit")]
    [InlineData("limit=51", "limit")]
    [InlineData("cursor=not-a-cursor", "cursor")]
    public async Task InvalidQuery_ReturnsValidationProblem(string query, string field)
    {
        await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, $"{Path}?{query}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be(field);
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Validation.Failed");
    }

    private async Task<PageBody> GetPageAsync(string uri)
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, uri);
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<PageBody>(TestContext.Current.CancellationToken))!;
    }

    private sealed record PageBody(List<NotificationBody> Items, string? NextCursor);

    private sealed record ActorBody(Guid Id, string Username, string DisplayName, string? AvatarUrl);

    private sealed record NotificationBody(
        Guid Id,
        string Type,
        ActorBody Actor,
        DateTimeOffset CreatedAt,
        bool IsRead,
        Guid? PostId = null,
        string? PostExcerpt = null,
        Guid? CommentId = null,
        string? CommentExcerpt = null);
}
