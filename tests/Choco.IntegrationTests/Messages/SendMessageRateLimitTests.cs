using System.Globalization;
using System.Net;

using Choco.Domain.Users;
using Choco.IntegrationTests.Feedback;
using Choco.IntegrationTests.Notifications;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Choco.IntegrationTests.Messages;

public sealed class SendMessageRateLimitApiFactory : ChocoApiFactory
{
    public const int PerMinute = 2;

    public SendMessageRateLimitApiFactory(PostgresFixture postgres) : base(postgres) { }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["RateLimiting:DirectMessagesPerMinute"] = PerMinute.ToString(CultureInfo.InvariantCulture)
            }));
    }
}

public sealed class SendMessageRateLimitTests : IClassFixture<SendMessageRateLimitApiFactory>, IAsyncLifetime
{
    private readonly SendMessageRateLimitApiFactory _factory;
    private readonly HttpClient _client;

    public SendMessageRateLimitTests(SendMessageRateLimitApiFactory factory)
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
    public async Task SendingMoreThanThePerMinuteLimit_ReturnsTooManyRequestsWithRetryAfterAndAddsNothing()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id-1", "username-1");
        User recipient = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        await SendAsync(recipient.Id, "identity-id-1", 1, HttpStatusCode.Created);
        await SendAsync(recipient.Id, "identity-id-1", 2, HttpStatusCode.Created);

        using HttpResponseMessage response = await SendOneAsync(recipient.Id, "identity-id-1", 3);

        await FeedbackRateLimitRequests.AssertTooManyRequestsAsync(response, TimeSpan.FromMinutes(1));
        (await MessageTestData.GetMessagesAsync(_factory)).Should().HaveCount(SendMessageRateLimitApiFactory.PerMinute);
    }

    [Fact]
    public async Task SendingMoreThanThePerMinuteLimit_DoesNotLimitOtherUsers()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        await NotificationTestData.AddUserAsync(_factory, "identity-id-4", "username-4");
        User recipient = await NotificationTestData.AddUserAsync(_factory, "identity-id-5", "username-5");
        await SendAsync(recipient.Id, "identity-id-3", 1, null);
        await SendAsync(recipient.Id, "identity-id-3", 2, null);
        await SendAsync(recipient.Id, "identity-id-3", 3, null);

        using HttpResponseMessage response = await SendOneAsync(recipient.Id, "identity-id-4", 4);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private async Task SendAsync(Guid recipientId, string identityId, int index, HttpStatusCode? expected)
    {
        using HttpResponseMessage response = await SendOneAsync(recipientId, identityId, index);

        if (expected is { } status)
        {
            response.StatusCode.Should().Be(status);
        }
    }

    private async Task<HttpResponseMessage> SendOneAsync(Guid recipientId, string identityId, int index)
    {
        using HttpRequestMessage request = MessageTestData.SendRequest(
            recipientId,
            new { clientMessageId = new Guid($"00000000-0000-0000-0000-{index:D12}"), content = "content" },
            identityId);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
