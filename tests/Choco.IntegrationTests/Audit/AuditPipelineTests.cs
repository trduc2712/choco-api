using System.Net;

using Choco.Domain.AuditLogs;
using Choco.Domain.Posts;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

namespace Choco.IntegrationTests.Audit;

public sealed class AuditPipelineTests(ChocoApiFactory factory) : AuditTestsBase(factory)
{
    [Fact]
    public async Task FailedCommand_RecordsNoEntry()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(
            HttpMethod.Delete, "/api/v1/posts/00000000-0000-0000-0000-000000000001");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadEntriesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ForbiddenCommand_RecordsNoEntry()
    {
        User author = await AddUserAsync("identity-id-2", "username-2");
        await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        using HttpRequestMessage request = Request(HttpMethod.Delete, $"/api/v1/posts/{post.Id}");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadEntriesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task LikePost_RecordsNoEntry()
    {
        User author = await AddUserAsync("identity-id-2", "username-2");
        await AddUserAsync("identity-id", "username");
        Post post = await AddPostAsync(author);
        using HttpRequestMessage request = Request(HttpMethod.Put, $"/api/v1/posts/{post.Id}/like");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.Should().BeTrue();
        (await ReadEntriesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Request_RecordsClientUserAgentAndTraceId()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(HttpMethod.Post, "/api/v1/posts", new { content = "content" });
        request.Headers.UserAgent.ParseAdd("user-agent/1.0");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        entry.ClientId.Should().Be(TestJwt.AuthorizedParty);
        entry.UserAgent.Should().Be("user-agent/1.0");
        entry.TraceId.Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task ForwardedForHeader_WhenForwardingIsOff_IsNotRecorded()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(HttpMethod.Post, "/api/v1/posts", new { content = "content" });
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.7");

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        entry.IpAddress.Should().NotBe(System.Net.IPAddress.Parse("203.0.113.7"));
    }

    [Fact]
    public async Task UserAgentOverMaxLength_IsCutAndTheCommandStillSucceeds()
    {
        await AddUserAsync("identity-id", "username");
        using HttpRequestMessage request = Request(HttpMethod.Post, "/api/v1/posts", new { content = "content" });
        request.Headers.TryAddWithoutValidation("User-Agent", new string('u', AuditLogEntry.UserAgentMaxLength + 1));

        using HttpResponseMessage response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        AuditLogEntry entry = (await ReadEntriesAsync()).Should().ContainSingle().Subject;
        entry.UserAgent.Should().Be(new string('u', AuditLogEntry.UserAgentMaxLength));
    }
}
