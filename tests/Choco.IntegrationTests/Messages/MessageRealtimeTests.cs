using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;

using Choco.Application.Abstractions.Messages;
using Choco.Contract.Messages;
using Choco.Domain.Conversations;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;
using Choco.IntegrationTests.Notifications;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Choco.IntegrationTests.Messages;

public sealed class FailingMessageRealtimeApiFactory(PostgresFixture postgres) : ChocoApiFactory(postgres)
{
    internal Guid? CommittedMessageAtPublish { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.Replace(
            ServiceDescriptor.Singleton<IMessageRealtimePublisher>(new FailingPublisher(this))));
    }

    private sealed class FailingPublisher(FailingMessageRealtimeApiFactory factory) : IMessageRealtimePublisher
    {
        public async Task PublishAsync(
            string senderIdentityId, string recipientIdentityId, MessageResponse message, CancellationToken cancellationToken)
        {
            factory.CommittedMessageAtPublish = await factory.ExecuteDbContextAsync(db =>
                db.Set<Message>().AsNoTracking()
                    .Where(row => row.Id == message.Id)
                    .Select(row => (Guid?)row.Id)
                    .SingleOrDefaultAsync(cancellationToken));

            throw new InvalidOperationException("Real-time delivery failed.");
        }
    }
}

public sealed class MessageRealtimeTests(ChocoApiFactory factory, FailingMessageRealtimeApiFactory failingFactory)
    : IClassFixture<ChocoApiFactory>, IClassFixture<FailingMessageRealtimeApiFactory>, IAsyncLifetime
{
    private const string SenderIdentity = "identity-id";
    private const string RecipientIdentity = "identity-id-2";
    private const string HubPath = "hubs/messages";
    private const string MessageReceived = "messageReceived";

    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(10);
    private static readonly Guid ClientMessageId = new("00000000-0000-0000-0000-000000000011");
    private static readonly Guid NextClientMessageId = new("00000000-0000-0000-0000-000000000012");

    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task NewMessage_DeliversTheWholePayloadToBothParticipants()
    {
        (User sender, User recipient) = await AddParticipantsAsync(factory);
        await using HubConnection senderConnection = CreateConnection(SenderIdentity);
        await using HubConnection recipientConnection = CreateConnection(RecipientIdentity);
        Channel<JsonElement> senderEvents = Listen(senderConnection);
        Channel<JsonElement> recipientEvents = Listen(recipientConnection);
        await senderConnection.StartAsync(TestContext.Current.CancellationToken);
        await recipientConnection.StartAsync(TestContext.Current.CancellationToken);

        MessageResponse sent = await SendAsync(_client, recipient.Id, ClientMessageId, "content");

        Message row = (await MessageTestData.GetMessagesAsync(factory)).Should().ContainSingle().Subject;
        sent.Id.Should().Be(row.Id);
        sent.ConversationId.Should().Be(row.ConversationId);
        sent.Sequence.Should().Be(1);
        sent.ClientMessageId.Should().Be(ClientMessageId);
        sent.SenderId.Should().Be(sender.Id);
        sent.Content.Should().Be("content");
        sent.CreatedAt.Should().Be(ChocoApiFactory.StartTime);
        AssertPayload(await ReadAsync(senderEvents), sent);
        AssertPayload(await ReadAsync(recipientEvents), sent);
    }

    [Fact]
    public async Task Retry_ReturnsTheSavedMessageAndDoesNotPublishItAgain()
    {
        (_, User recipient) = await AddParticipantsAsync(factory);
        await using HubConnection connection = CreateConnection(RecipientIdentity);
        Channel<JsonElement> events = Listen(connection);
        await connection.StartAsync(TestContext.Current.CancellationToken);
        MessageResponse original = await SendAsync(_client, recipient.Id, ClientMessageId, "content");
        AssertPayload(await ReadAsync(events), original);

        MessageResponse retry = await SendAsync(_client, recipient.Id, ClientMessageId, "retry content");
        MessageResponse next = await SendAsync(_client, recipient.Id, NextClientMessageId, "content-2");

        retry.Should().Be(original);
        next.Sequence.Should().Be(2);
        AssertPayload(await ReadAsync(events), next);
    }

    [Fact]
    public async Task FailedPublish_LeavesTheCommittedMessageAndReturnsCreated()
    {
        await failingFactory.ResetAsync();
        (_, User recipient) = await AddParticipantsAsync(failingFactory);
        using HttpClient client = failingFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        MessageResponse sent = await SendAsync(client, recipient.Id, ClientMessageId, "content");

        Message row = (await MessageTestData.GetMessagesAsync(failingFactory)).Should().ContainSingle().Subject;
        row.Id.Should().Be(sent.Id);
        row.Content.Should().Be("content");
        failingFactory.CommittedMessageAtPublish.Should().Be(sent.Id);
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
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.WebSocketFactory = async (context, cancellationToken) =>
                {
                    Uri uri = token is null
                        ? context.Uri
                        : new Uri($"{context.Uri}&access_token={Uri.EscapeDataString(token)}");

                    return await factory.Server.CreateWebSocketClient().ConnectAsync(uri, cancellationToken);
                };
                options.AccessTokenProvider = () => Task.FromResult(token);
            })
            .Build();
    }

    private static async Task<(User Sender, User Recipient)> AddParticipantsAsync(ChocoApiFactory apiFactory)
    {
        User sender = await NotificationTestData.AddUserAsync(apiFactory, SenderIdentity, "username");
        User recipient = await NotificationTestData.AddUserAsync(apiFactory, RecipientIdentity, "username-2");

        return (sender, recipient);
    }

    private static async Task<MessageResponse> SendAsync(HttpClient client, Guid recipientId, Guid clientMessageId, string content)
    {
        using HttpRequestMessage request = MessageTestData.SendRequest(recipientId, new { clientMessageId, content }, SenderIdentity);
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<MessageResponse>(TestContext.Current.CancellationToken))!;
    }

    private static Channel<JsonElement> Listen(HubConnection connection)
    {
        Channel<JsonElement> events = Channel.CreateUnbounded<JsonElement>();
        connection.On<JsonElement>(MessageReceived, payload => events.Writer.TryWrite(payload));
        return events;
    }

    private static async Task<JsonElement> ReadAsync(Channel<JsonElement> events)
    {
        return await events.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(DeliveryTimeout, TestContext.Current.CancellationToken);
    }

    private static void AssertPayload(JsonElement payload, MessageResponse expected)
    {
        payload.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "id", "conversationId", "sequence", "clientMessageId", "senderId", "content", "createdAt");
        payload.GetProperty("id").GetGuid().Should().Be(expected.Id);
        payload.GetProperty("conversationId").GetGuid().Should().Be(expected.ConversationId);
        payload.GetProperty("sequence").GetInt64().Should().Be(expected.Sequence);
        payload.GetProperty("clientMessageId").GetGuid().Should().Be(expected.ClientMessageId);
        payload.GetProperty("senderId").GetGuid().Should().Be(expected.SenderId);
        payload.GetProperty("content").GetString().Should().Be(expected.Content);
        payload.GetProperty("createdAt").GetDateTimeOffset().Should().Be(expected.CreatedAt);
    }
}
