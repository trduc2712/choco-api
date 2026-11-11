using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Choco.Domain.AuditLogs;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Audit;

public sealed class ForwardedHeadersTests(ForwardedHeadersApiFactory factory)
    : IClassFixture<ForwardedHeadersApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ForwardedFor_RecordsTheAddressTheTrustedProxyAdded_NotWhatTheClientSent()
    {
        await AddUserAsync();
        using HttpRequestMessage request = CreatePostRequest();
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.9, 203.0.113.7");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        AuditLogEntry entry = await ReadSingleEntryAsync();
        entry.IpAddress.Should().Be(IPAddress.Parse("203.0.113.7"));
    }

    [Fact]
    public async Task ForwardedProto_MakesTheLocationOfACreatedResourceHttps()
    {
        await AddUserAsync();
        using HttpRequestMessage request = CreatePostRequest();
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.Scheme.Should().Be("https");
    }

    private static HttpRequestMessage CreatePostRequest()
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/posts")
        {
            Content = JsonContent.Create(new { content = "content" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));

        return request;
    }

    private Task AddUserAsync()
    {
        User user = User.Create("identity-id", "username", "Display Name", null);

        return factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(user);
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private Task<AuditLogEntry> ReadSingleEntryAsync()
    {
        return factory.ExecuteDbContextAsync(dbContext => dbContext.Set<AuditLogEntry>()
            .AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken));
    }
}
