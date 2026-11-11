using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.FeedbackEntries;
using Choco.IntegrationTests.Notifications;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Choco.IntegrationTests.Feedback;

public sealed class FeedbackHourlyLimitApiFactory : ChocoApiFactory
{
    public const int PerHour = 2;

    public FeedbackHourlyLimitApiFactory(PostgresFixture postgres) : base(postgres) { }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["RateLimiting:FeedbackPerHour"] = PerHour.ToString(CultureInfo.InvariantCulture),
                ["RateLimiting:FeedbackPerDay"] = "100"
            }));
    }
}

public sealed class FeedbackDailyLimitApiFactory : ChocoApiFactory
{
    public const int PerDay = 2;

    public FeedbackDailyLimitApiFactory(PostgresFixture postgres) : base(postgres) { }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["RateLimiting:FeedbackPerHour"] = "100",
                ["RateLimiting:FeedbackPerDay"] = PerDay.ToString(CultureInfo.InvariantCulture)
            }));
    }
}

public sealed class FeedbackHourlyLimitTests : IClassFixture<FeedbackHourlyLimitApiFactory>, IAsyncLifetime
{
    private readonly FeedbackHourlyLimitApiFactory _factory;
    private readonly HttpClient _client;

    public FeedbackHourlyLimitTests(FeedbackHourlyLimitApiFactory factory)
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
    public async Task SendingMoreThanTheHourlyLimit_ReturnsTooManyRequestsWithRetryAfter()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id-1", "username-1");
        await FeedbackRateLimitRequests.SendAsync(_client, "identity-id-1", FeedbackHourlyLimitApiFactory.PerHour, HttpStatusCode.Created);

        using HttpResponseMessage response = await FeedbackRateLimitRequests.SendOneAsync(_client, "identity-id-1");

        await FeedbackRateLimitRequests.AssertTooManyRequestsAsync(response, TimeSpan.FromHours(1));
        (await CountEntriesAsync()).Should().Be(FeedbackHourlyLimitApiFactory.PerHour);
    }

    [Fact]
    public async Task SendingMoreThanTheHourlyLimit_DoesNotLimitOtherUsers()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        await FeedbackRateLimitRequests.SendAsync(_client, "identity-id-2", FeedbackHourlyLimitApiFactory.PerHour + 1, null);

        using HttpResponseMessage response = await FeedbackRateLimitRequests.SendOneAsync(_client, "identity-id-3");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task SendingMoreThanTheHourlyLimit_DoesNotLimitOtherEndpoints()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id-4", "username-4");
        await FeedbackRateLimitRequests.SendAsync(_client, "identity-id-4", FeedbackHourlyLimitApiFactory.PerHour + 1, null);
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, "/api/v1/me", "identity-id-4");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private Task<int> CountEntriesAsync()
    {
        return _factory.ExecuteDbContextAsync(db => db.Set<FeedbackEntry>().CountAsync(TestContext.Current.CancellationToken));
    }
}

public sealed class FeedbackDailyLimitTests : IClassFixture<FeedbackDailyLimitApiFactory>, IAsyncLifetime
{
    private readonly FeedbackDailyLimitApiFactory _factory;
    private readonly HttpClient _client;

    public FeedbackDailyLimitTests(FeedbackDailyLimitApiFactory factory)
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
    public async Task SendingMoreThanTheDailyLimit_ReturnsTooManyRequestsWithRetryAfter()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        await FeedbackRateLimitRequests.SendAsync(_client, "identity-id", FeedbackDailyLimitApiFactory.PerDay, HttpStatusCode.Created);

        using HttpResponseMessage response = await FeedbackRateLimitRequests.SendOneAsync(_client, "identity-id");

        await FeedbackRateLimitRequests.AssertTooManyRequestsAsync(response, TimeSpan.FromDays(1));
    }
}

internal static class FeedbackRateLimitRequests
{
    public static async Task SendAsync(HttpClient client, string identityId, int count, HttpStatusCode? expected)
    {
        for (int index = 0; index < count; index++)
        {
            using HttpResponseMessage response = await SendOneAsync(client, identityId);

            if (expected is { } status)
            {
                response.StatusCode.Should().Be(status);
            }
        }
    }

    public static async Task<HttpResponseMessage> SendOneAsync(HttpClient client, string identityId)
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Post, "/api/v1/feedback", identityId);
        request.Content = JsonContent.Create(new
        {
            category = "bug",
            message = "feedback message",
            context = new
            {
                path = "/path",
                locale = "en",
                theme = "system",
                userAgent = "user-agent",
                viewport = new { width = 1280, height = 720 },
                appVersion = "app-version"
            }
        });

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static async Task AssertTooManyRequestsAsync(HttpResponseMessage response, TimeSpan window)
    {
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        response.Headers.RetryAfter!.Delta.Should().BePositive().And.BeLessThanOrEqualTo(window);
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("RateLimit.Exceeded");
    }
}
