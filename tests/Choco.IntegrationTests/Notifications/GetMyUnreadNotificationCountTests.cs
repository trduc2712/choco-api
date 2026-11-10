using System.Net;
using System.Net.Http.Json;

using Choco.Domain.Notifications;
using Choco.Domain.Users;

namespace Choco.IntegrationTests.Notifications;

public sealed class GetMyUnreadNotificationCountTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string Path = "/api/v1/me/notifications/unread-count";

    private static readonly DateTimeOffset FirstAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondAt = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ThirdAt = new(2026, 1, 3, 0, 0, 0, TimeSpan.Zero);

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetMyUnreadNotificationCountTests(ChocoApiFactory factory)
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
    public async Task CallerWithoutNotifications_GetsZero()
    {
        await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");

        int count = await GetCountAsync();

        count.Should().Be(0);
    }

    [Fact]
    public async Task Count_CoversOnlyUnreadNotificationsOfTheCallerWithExistingActors()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User other = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        User actor = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        User deletedActor = await NotificationTestData.AddUserAsync(_factory, "identity-id-4", "username-4");
        await NotificationTestData.AddNotificationAsync(_factory, caller, actor, FirstAt);
        await NotificationTestData.AddNotificationAsync(_factory, caller, actor, SecondAt);
        Notification read = await NotificationTestData.AddNotificationAsync(_factory, caller, actor, ThirdAt);
        await NotificationTestData.AddNotificationAsync(_factory, caller, deletedActor, FirstAt);
        await NotificationTestData.AddNotificationAsync(_factory, other, actor, FirstAt);
        await _factory.ExecuteDbContextAsync(db =>
        {
            Notification tracked = db.Set<Notification>().Single(notification => notification.Id == read.Id);
            tracked.MarkRead(ThirdAt);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        await NotificationTestData.DeleteUserAsync(_factory, deletedActor);

        int count = await GetCountAsync();

        count.Should().Be(2);
    }

    private async Task<int> GetCountAsync()
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, Path);
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<CountBody>(TestContext.Current.CancellationToken))!.Count;
    }

    private sealed record CountBody(int Count);
}
