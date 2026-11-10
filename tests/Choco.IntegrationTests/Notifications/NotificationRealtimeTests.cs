using System.Net;
using System.Text.Json;
using System.Threading.Channels;

using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Choco.IntegrationTests.Notifications;

public sealed class NotificationRealtimeTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string HubPath = "hubs/notifications";
    private const string NotificationReceivedEvent = "notificationReceived";

    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(10);

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public NotificationRealtimeTests(ChocoApiFactory factory)
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
    public async Task Following_DeliversTheNotificationToTheConnectedRecipient()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User target = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        await using HubConnection connection = CreateConnection("identity-id-2");
        TaskCompletionSource<JsonElement> received = Listen(connection);
        await connection.StartAsync(TestContext.Current.CancellationToken);

        await FollowAsync(target.Id);

        JsonElement payload = await received.Task.WaitAsync(DeliveryTimeout, TestContext.Current.CancellationToken);
        payload.GetProperty("id").GetGuid().Should().Be(
            (await NotificationTestData.GetNotificationsAsync(_factory, target.Id)).Should().ContainSingle().Subject.Id);
        payload.GetProperty("type").GetString().Should().Be("Followed");
        payload.GetProperty("actor").GetProperty("id").GetGuid().Should().Be(caller.Id);
        payload.GetProperty("actor").GetProperty("username").GetString().Should().Be("username");
        payload.GetProperty("createdAt").GetDateTimeOffset().Should().Be(_factory.TimeProvider.GetUtcNow());
        payload.GetProperty("isRead").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Following_DoesNotDeliverTheNotificationToOtherUsers()
    {
        await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User target = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        User other = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        await using HubConnection targetConnection = CreateConnection("identity-id-2");
        await using HubConnection otherConnection = CreateConnection("identity-id-3");
        Channel<JsonElement> targetReceived = ListenToChannel(targetConnection);
        Channel<JsonElement> otherReceived = ListenToChannel(otherConnection);
        await targetConnection.StartAsync(TestContext.Current.CancellationToken);
        await otherConnection.StartAsync(TestContext.Current.CancellationToken);

        await FollowAsync(target.Id);
        await FollowAsync(other.Id);

        JsonElement otherFirst = await ReadAsync(otherReceived);
        JsonElement targetFirst = await ReadAsync(targetReceived);
        Guid otherNotificationId = (await NotificationTestData.GetNotificationsAsync(_factory, other.Id)).Should().ContainSingle().Subject.Id;
        Guid targetNotificationId = (await NotificationTestData.GetNotificationsAsync(_factory, target.Id)).Should().ContainSingle().Subject.Id;
        otherFirst.GetProperty("id").GetGuid().Should().Be(otherNotificationId);
        targetFirst.GetProperty("id").GetGuid().Should().Be(targetNotificationId);
        otherReceived.Reader.TryRead(out _).Should().BeFalse();
        targetReceived.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task ConnectionWithoutToken_IsRejected()
    {
        await using HubConnection connection = CreateConnection(null);

        Func<Task> start = () => connection.StartAsync(TestContext.Current.CancellationToken);

        (await start.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private HubConnection CreateConnection(string? subject)
    {
        string? token = subject is null ? null : TestJwt.Create(DateTimeOffset.UtcNow, subject: subject);

        return new HubConnectionBuilder()
            .WithUrl(new Uri(_client.BaseAddress!, HubPath), options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.WebSocketFactory = async (context, cancellationToken) =>
                {
                    Uri uri = token is null
                        ? context.Uri
                        : new Uri($"{context.Uri}&access_token={Uri.EscapeDataString(token)}");

                    return await _factory.Server.CreateWebSocketClient().ConnectAsync(uri, cancellationToken);
                };
                options.AccessTokenProvider = () => Task.FromResult(token);
            })
            .Build();
    }

    private static TaskCompletionSource<JsonElement> Listen(HubConnection connection)
    {
        TaskCompletionSource<JsonElement> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>(NotificationReceivedEvent, payload => received.TrySetResult(payload));

        return received;
    }

    private static Channel<JsonElement> ListenToChannel(HubConnection connection)
    {
        Channel<JsonElement> received = Channel.CreateUnbounded<JsonElement>();
        connection.On<JsonElement>(NotificationReceivedEvent, payload => received.Writer.TryWrite(payload));

        return received;
    }

    private static async Task<JsonElement> ReadAsync(Channel<JsonElement> received)
    {
        return await received.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(DeliveryTimeout, TestContext.Current.CancellationToken);
    }

    private async Task FollowAsync(Guid userId)
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Put, $"/api/v1/users/{userId}/follow");
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
