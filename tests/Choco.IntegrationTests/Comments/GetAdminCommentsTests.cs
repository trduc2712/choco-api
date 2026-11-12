using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Contract.Comments;
using Choco.Domain.Comments;
using Choco.Domain.Posts;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;

namespace Choco.IntegrationTests.Comments;

public sealed class GetAdminCommentsTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string Path = "/api/v1/admin/comments";

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetAdminCommentsTests(ChocoApiFactory factory)
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
    public async Task NoToken_ReturnsUnauthorized()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, Path);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Member_ReturnsForbidden()
    {
        await AddUserAsync("identity-id", "username");

        using HttpResponseMessage response = await GetAsync(Path);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Permission.Denied");
    }

    [Fact]
    public async Task UserWithAnotherPermissionButNotReadAnyContent_ReturnsForbidden()
    {
        await AddCallerAsync(Permission.ReadAnyUser, Permission.DeleteAnyComment, Permission.HandleReports);

        using HttpResponseMessage response = await GetAsync(Path);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InvalidQueryFromCallerWithoutPermission_ReturnsForbiddenBeforeValidating()
    {
        await AddUserAsync("identity-id", "username");

        using HttpResponseMessage response = await GetAsync($"{Path}?page=0");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UserWithOnlyReadAnyContent_ListsComments()
    {
        User caller = await AddCallerAsync(Permission.ReadAnyContent);
        Post post = await AddPostAsync(caller);
        await AddCommentAsync(post, caller, "content");

        JsonElement body = await GetJsonAsync(Path);

        body.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Comments_AreListedNewestFirstAcrossPostsWithTheirFields()
    {
        User caller = await AddCallerAsync(Permission.ReadAnyContent);
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(caller);
        Post otherPost = await AddPostAsync(caller);
        Comment older = await AddCommentAsync(post, caller, "content-1");
        Comment newer = await AddCommentAsync(otherPost, author, "content-2");

        JsonElement body = await GetJsonAsync(Path);

        JsonElement[] items = body.GetProperty("items").EnumerateArray().ToArray();
        items.Select(item => item.GetProperty("id").GetGuid()).Should().Equal(newer.Id, older.Id);
        JsonElement first = items[0];
        first.GetProperty("postId").GetGuid().Should().Be(otherPost.Id);
        first.GetProperty("content").GetString().Should().Be("content-2");
        first.GetProperty("author").GetProperty("id").GetGuid().Should().Be(author.Id);
        first.GetProperty("author").GetProperty("username").GetString().Should().Be("username-2");
        first.GetProperty("isDeleted").GetBoolean().Should().BeFalse();
        first.GetProperty("deletedAt").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(20);
    }

    [Fact]
    public async Task DeletedComments_AreHiddenByDefault()
    {
        User caller = await AddCallerAsync(Permission.ReadAnyContent);
        Post post = await AddPostAsync(caller);
        Comment deleted = await AddCommentAsync(post, caller, "content-1");
        Comment visible = await AddCommentAsync(post, caller, "content-2");
        await DeleteCommentAsync(deleted.Id);

        JsonElement body = await GetJsonAsync(Path);

        body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).Should().Equal(visible.Id);
        body.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task StateDeleted_ListsOnlyDeletedCommentsWithWhenTheyWereDeleted()
    {
        User caller = await AddCallerAsync(Permission.ReadAnyContent);
        Post post = await AddPostAsync(caller);
        Comment deleted = await AddCommentAsync(post, caller, "content-1");
        await AddCommentAsync(post, caller, "content-2");
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));
        await DeleteCommentAsync(deleted.Id);

        JsonElement body = await GetJsonAsync($"{Path}?state=deleted");

        JsonElement item = body.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("id").GetGuid().Should().Be(deleted.Id);
        item.GetProperty("isDeleted").GetBoolean().Should().BeTrue();
        item.GetProperty("deletedAt").GetDateTimeOffset().Should().Be(_factory.TimeProvider.GetUtcNow());
    }

    [Fact]
    public async Task CommentsOnADeletedPost_AreStillListedWithThePostId()
    {
        User caller = await AddCallerAsync(Permission.ReadAnyContent);
        Post post = await AddPostAsync(caller);
        Comment comment = await AddCommentAsync(post, caller, "content");
        await DeletePostAsync(post.Id);

        JsonElement body = await GetJsonAsync(Path);

        JsonElement item = body.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("id").GetGuid().Should().Be(comment.Id);
        item.GetProperty("postId").GetGuid().Should().Be(post.Id);
    }

    [Fact]
    public async Task CommentsOfADeletedAuthor_AreListedWithTheirUsername()
    {
        User caller = await AddCallerAsync(Permission.ReadAnyContent);
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(caller);
        await AddCommentAsync(post, author, "content");
        await DeleteUserAsync(author.Id);

        JsonElement body = await GetJsonAsync($"{Path}?authorId={author.Id}");

        JsonElement item = body.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("author").GetProperty("username").GetString().Should().Be("username-2");
    }

    [Fact]
    public async Task Filter_ByAuthorAndByPost_ListsOnlyMatchingComments()
    {
        User caller = await AddCallerAsync(Permission.ReadAnyContent);
        User author = await AddUserAsync("identity-id-2", "username-2");
        Post post = await AddPostAsync(caller);
        Post otherPost = await AddPostAsync(caller);
        await AddCommentAsync(post, caller, "content-1");
        Comment byAuthorOnPost = await AddCommentAsync(post, author, "content-2");
        Comment byAuthorOnOtherPost = await AddCommentAsync(otherPost, author, "content-3");

        JsonElement byAuthor = await GetJsonAsync($"{Path}?authorId={author.Id}");
        JsonElement byPost = await GetJsonAsync($"{Path}?postId={otherPost.Id}");
        JsonElement both = await GetJsonAsync($"{Path}?authorId={author.Id}&postId={post.Id}");

        static IEnumerable<Guid> Ids(JsonElement body) => body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid());

        Ids(byAuthor).Should().Equal(byAuthorOnOtherPost.Id, byAuthorOnPost.Id);
        Ids(byPost).Should().Equal(byAuthorOnOtherPost.Id);
        Ids(both).Should().Equal(byAuthorOnPost.Id);
    }

    [Fact]
    public async Task Filter_ByTimeRange_IncludesFromAndExcludesTo()
    {
        User caller = await AddCallerAsync(Permission.ReadAnyContent);
        Post post = await AddPostAsync(caller);
        Comment first = await AddCommentAsync(post, caller, "content-1", TimeSpan.FromHours(1));
        Comment second = await AddCommentAsync(post, caller, "content-2", TimeSpan.FromHours(1));
        Comment third = await AddCommentAsync(post, caller, "content-3", TimeSpan.FromHours(1));

        string from = Uri.EscapeDataString(first.CreatedAt.ToString("O"));
        string to = Uri.EscapeDataString(third.CreatedAt.ToString("O"));
        JsonElement body = await GetJsonAsync($"{Path}?from={from}&to={to}");

        body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).Should().Equal(second.Id, first.Id);
    }

    [Fact]
    public async Task Search_RequiresEveryWordIgnoringCaseAndAccents()
    {
        User caller = await AddCallerAsync(Permission.ReadAnyContent);
        Post post = await AddPostAsync(caller);
        Comment both = await AddCommentAsync(post, caller, "Chào mừng đến Hà Nội");
        await AddCommentAsync(post, caller, "Chào mừng bạn");
        await AddCommentAsync(post, caller, "Hà Nội");

        JsonElement body = await GetJsonAsync($"{Path}?q=chao%20ha%20noi");

        body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).Should().Equal(both.Id);
    }

    [Fact]
    public async Task Search_TreatsWildcardCharactersAsText()
    {
        User caller = await AddCallerAsync(Permission.ReadAnyContent);
        Post post = await AddPostAsync(caller);
        await AddCommentAsync(post, caller, "content");
        Comment literal = await AddCommentAsync(post, caller, "100% sure");

        JsonElement percent = await GetJsonAsync($"{Path}?q=%25");
        JsonElement underscore = await GetJsonAsync($"{Path}?q=_");

        percent.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).Should().Equal(literal.Id);
        underscore.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Comments_AreReturnedByPageWithoutOverlapAndWithTheTotal()
    {
        User caller = await AddCallerAsync(Permission.ReadAnyContent);
        Post post = await AddPostAsync(caller);
        for (int index = 1; index <= 5; index++)
        {
            await AddCommentAsync(post, caller, $"content-{index}");
        }

        JsonElement first = await GetJsonAsync($"{Path}?pageSize=2");
        JsonElement last = await GetJsonAsync($"{Path}?pageSize=2&page=3");
        JsonElement pastEnd = await GetJsonAsync($"{Path}?pageSize=2&page=99");

        static IEnumerable<string?> Contents(JsonElement body) => body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("content").GetString());

        Contents(first).Should().Equal("content-5", "content-4");
        Contents(last).Should().Equal("content-1");
        pastEnd.GetProperty("items").GetArrayLength().Should().Be(0);
        first.GetProperty("totalCount").GetInt32().Should().Be(5);
        pastEnd.GetProperty("totalCount").GetInt32().Should().Be(5);
    }

    [Theory]
    [InlineData("state=Visible", "state")]
    [InlineData("state=archived", "state")]
    [InlineData("authorId=00000000-0000-0000-0000-000000000000", "authorId")]
    [InlineData("postId=00000000-0000-0000-0000-000000000000", "postId")]
    [InlineData("from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z", "to")]
    [InlineData("page=0", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=51", "pageSize")]
    public async Task InvalidQuery_ReturnsBadRequestNamingTheField(string query, string field)
    {
        await AddCallerAsync(Permission.ReadAnyContent);

        using HttpResponseMessage response = await GetAsync($"{Path}?{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().BeEquivalentTo(field);
    }

    [Fact]
    public async Task SearchOverMaxLength_ReturnsBadRequestAndAtMaxLengthIsAccepted()
    {
        await AddCallerAsync(Permission.ReadAnyContent);

        using HttpResponseMessage tooLong = await GetAsync($"{Path}?q={new string('a', GetAdminCommentsQuery.SearchMaxLength + 1)}");
        using HttpResponseMessage atLimit = await GetAsync($"{Path}?q={new string('a', GetAdminCommentsQuery.SearchMaxLength)}");

        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        atLimit.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<HttpResponseMessage> GetAsync(string path)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: "identity-id"));

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<JsonElement> GetJsonAsync(string path)
    {
        using HttpResponseMessage response = await GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private async Task<User> AddCallerAsync(params string[] permissions)
    {
        Role callerRole = Role.Create("Caller Role", permissions);
        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(callerRole);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return await AddUserAsync("identity-id", "username", callerRole.Id);
    }

    private async Task<User> AddUserAsync(string identityId, string username, Guid? roleId = null)
    {
        User user = User.Create(identityId, username, "Display Name", null);

        if (roleId is not null)
        {
            user.AssignRole(roleId.Value, null, ChocoApiFactory.StartTime);
        }

        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(user);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return user;
    }

    private async Task<Post> AddPostAsync(User author)
    {
        Post post = Post.Create("content", author.Id);

        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(post);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return post;
    }

    private async Task<Comment> AddCommentAsync(Post post, User author, string content, TimeSpan? advance = null)
    {
        _factory.TimeProvider.Advance(advance ?? TimeSpan.FromSeconds(1));
        Comment comment = Comment.Create(post.Id, author.Id, content);

        await _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(comment);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return comment;
    }

    private Task DeleteCommentAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<Comment>().Single(comment => comment.Id == id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private Task DeletePostAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<Post>().Single(post => post.Id == id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private Task DeleteUserAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Remove(dbContext.Set<User>().Single(user => user.Id == id));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }
}
