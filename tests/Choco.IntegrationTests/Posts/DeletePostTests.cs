using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Posts;
using Choco.Domain.Roles;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Posts;

public sealed class DeletePostTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownPostId = new("00000000-0000-0000-0000-000000000001");

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public DeletePostTests(ChocoApiFactory factory)
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
        User author = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        using HttpRequestMessage request = new(HttpMethod.Delete, $"/api/posts/{post.Id}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Author_DeletesPost()
    {
        User author = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        _factory.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        using HttpRequestMessage request = CreateRequest(post.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Post saved = await GetPostAsync(post.Id);
        saved.IsDeleted.Should().BeTrue();
        saved.DeletedAt.Should().Be(ChocoApiFactory.StartTime.AddMinutes(1));
    }

    [Fact]
    public async Task AnotherMember_ReturnsForbiddenAndKeepsPost()
    {
        User author = await AddUserAsync("identity-id-2", "username-2");
        await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        using HttpRequestMessage request = CreateRequest(post.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Post.NotAuthor");
        (await GetPostAsync(post.Id)).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Administrator_DeletesAnotherUsersPost()
    {
        User author = await AddUserAsync("identity-id-2", "username-2");
        Guid adminRoleId = await _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .Where(role => role.SystemKey == SystemRoles.Admin)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken));
        await AddUserAsync("identity-id", "username", adminRoleId);
        Post post = await AddPostAsync(author);
        using HttpRequestMessage request = CreateRequest(post.Id);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetPostAsync(post.Id)).IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task UnknownPost_ReturnsNotFound()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = CreateRequest(UnknownPostId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "Post.NotFound");
    }

    [Fact]
    public async Task DeletedPost_ReturnsNotFound()
    {
        User author = await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        using HttpRequestMessage first = CreateRequest(post.Id);
        using HttpResponseMessage firstResponse = await _client.SendAsync(first, TestContext.Current.CancellationToken);
        using HttpRequestMessage second = CreateRequest(post.Id);

        using HttpResponseMessage secondResponse = await _client.SendAsync(second, TestContext.Current.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ShouldBeProblemAsync(secondResponse, HttpStatusCode.NotFound, "Post.NotFound");
    }

    private static HttpRequestMessage CreateRequest(Guid id)
    {
        HttpRequestMessage request = new(HttpMethod.Delete, $"/api/posts/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));

        return request;
    }

    private static async Task ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be(code);
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

    private Task<Post> GetPostAsync(Guid id)
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Post>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(post => post.Id == id, TestContext.Current.CancellationToken));
    }
}
