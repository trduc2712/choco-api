using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Choco.IntegrationTests.RateLimiting;

public sealed class RateLimitingTests : IClassFixture<RateLimitingApiFactory>, IAsyncLifetime
{
    private readonly RateLimitingApiFactory _factory;
    private readonly HttpClient _client;

    public RateLimitingTests(RateLimitingApiFactory factory)
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
    public async Task UserOverLimit_ReturnsTooManyRequests()
    {
        await AddUserAsync("identity-id", "username");
        await UseUpLimitAsync("identity-id");
        using HttpRequestMessage request = CreateMeRequest("identity-id");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        response.Headers.RetryAfter!.Delta.Should().BePositive();
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Status.Should().Be(StatusCodes.Status429TooManyRequests);
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("RateLimit.Exceeded");
    }

    [Fact]
    public async Task UserOverLimit_DoesNotLimitOtherUser()
    {
        await AddUserAsync("identity-id-1", "username-1");
        await AddUserAsync("identity-id-2", "username-2");
        await UseUpLimitAsync("identity-id-1");
        using HttpRequestMessage request = CreateMeRequest("identity-id-2");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnonymousRequestsOverLimit_AreNotLimited()
    {
        await SendHealthChecksUpToLimitAsync();

        using HttpResponseMessage response = await _client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task UseUpLimitAsync(string identityId)
    {
        for (int i = 0; i < RateLimitingApiFactory.TokenLimit; i++)
        {
            using HttpRequestMessage request = CreateMeRequest(identityId);
            using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    private async Task SendHealthChecksUpToLimitAsync()
    {
        for (int i = 0; i < RateLimitingApiFactory.TokenLimit; i++)
        {
            using HttpResponseMessage response = await _client.GetAsync("/health", TestContext.Current.CancellationToken);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    private Task AddUserAsync(string identityId, string username)
    {
        return _factory.ExecuteDbContextAsync(dbContext =>
        {
            dbContext.Add(User.Create(identityId, username, null, null));
            return dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
    }

    private static HttpRequestMessage CreateMeRequest(string identityId)
    {
        HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: identityId));

        return request;
    }
}
