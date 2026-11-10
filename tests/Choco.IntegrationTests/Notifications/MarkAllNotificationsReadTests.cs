using System.Net;

using Choco.Domain.Notifications;
using Choco.Domain.Users;

namespace Choco.IntegrationTests.Notifications;

public sealed class MarkAllNotificationsReadTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string Path = "/api/v1/me/notifications/read";

    private static readonly DateTimeOffset CreatedAt = new(2025, 12, 31, 0, 0, 0, TimeSpan.Zero);

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public MarkAllNotificationsReadTests(ChocoApiFactory factory)
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
        using HttpRequestMessage request = new(HttpMethod.Put, Path);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnreadNotificationsOfTheCaller_AreMarkedRead()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User other = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        User actor = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        await NotificationTestData.AddNotificationAsync(_factory, caller, actor, CreatedAt);
        await NotificationTestData.AddNotificationAsync(_factory, caller, actor, CreatedAt.AddDays(1));
        await NotificationTestData.AddNotificationAsync(_factory, other, actor, CreatedAt);

        HttpStatusCode status = await SendAsync();

        status.Should().Be(HttpStatusCode.NoContent);
        (await NotificationTestData.GetNotificationsAsync(_factory, caller.Id))
            .Should().OnlyContain(notification => notification.ReadAt == _factory.TimeProvider.GetUtcNow());
        (await NotificationTestData.GetNotificationsAsync(_factory, other.Id))
            .Should().ContainSingle().Which.ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task RepeatedRequest_KeepsTheFirstReadTime()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User actor = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        await NotificationTestData.AddNotificationAsync(_factory, caller, actor, CreatedAt);
        DateTimeOffset firstReadAt = _factory.TimeProvider.GetUtcNow();
        await SendAsync();
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));

        HttpStatusCode status = await SendAsync();

        status.Should().Be(HttpStatusCode.NoContent);
        (await NotificationTestData.GetNotificationsAsync(_factory, caller.Id)).Should().ContainSingle()
            .Which.ReadAt.Should().Be(firstReadAt);
    }

    [Fact]
    public async Task CallerWithoutNotifications_ReturnsNoContent()
    {
        await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");

        HttpStatusCode status = await SendAsync();

        status.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<HttpStatusCode> SendAsync()
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Put, Path);
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        return response.StatusCode;
    }
}
