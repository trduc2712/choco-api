using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Conversations;
using Choco.Domain.Users;
using Choco.IntegrationTests.Notifications;

namespace Choco.IntegrationTests.Messages;

public sealed class SendMessageConcurrencyTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const int Sends = 10;

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public SendMessageConcurrencyTests(ChocoApiFactory factory)
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
    public async Task FirstMessagesOfBothUsersAtTheSameTime_ShareOneConversation()
    {
        User user = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User other = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");

        HttpResponseMessage[] responses = await Task.WhenAll(
            SendAsync(other.Id, new Guid("00000000-0000-0000-0000-00000000000a"), "identity-id"),
            SendAsync(user.Id, new Guid("00000000-0000-0000-0000-00000000000b"), "identity-id-2"));

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Created);
        Conversation conversation = (await MessageTestData.GetConversationsAsync(_factory)).Should().ContainSingle().Subject;
        conversation.LastMessageSequence.Should().Be(2);
        List<Message> messages = await MessageTestData.GetMessagesAsync(_factory);
        messages.Should().OnlyContain(message => message.ConversationId == conversation.Id);
        messages.Select(message => message.Sequence).Should().Equal(1, 2);
        messages.Select(message => message.SenderId).Should().BeEquivalentTo([user.Id, other.Id]);
        DisposeAll(responses);
    }

    [Fact]
    public async Task ManyMessagesAtTheSameTime_GetEverySequenceOnce()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User recipient = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");

        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(1, Sends)
            .Select(index => SendAsync(recipient.Id, new Guid($"00000000-0000-0000-0000-{index:D12}"), "identity-id")));

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Created);
        (await MessageTestData.GetConversationsAsync(_factory)).Should().ContainSingle()
            .Which.LastMessageSequence.Should().Be(Sends);
        (await MessageTestData.GetMessagesAsync(_factory)).Select(message => message.Sequence)
            .Should().Equal(Enumerable.Range(1, Sends).Select(sequence => (long)sequence));
        DisposeAll(responses);
    }

    [Fact]
    public async Task SameClientMessageIdAtTheSameTime_SavesOneMessageAndReturnsItToEverySend()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User recipient = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Guid clientMessageId = new("4b6f2c1a-8d3e-4f5a-9b7c-1e2d3f4a5b6c");

        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(1, Sends)
            .Select(_ => SendAsync(recipient.Id, clientMessageId, "identity-id")));

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Created);
        Message message = (await MessageTestData.GetMessagesAsync(_factory)).Should().ContainSingle().Subject;
        Guid[] returnedIds = await Task.WhenAll(responses.Select(ReadIdAsync));
        returnedIds.Should().OnlyContain(id => id == message.Id);
        DisposeAll(responses);
    }

    private Task<HttpResponseMessage> SendAsync(Guid recipientId, Guid clientMessageId, string identityId)
    {
        return Task.Run(async () =>
        {
            using HttpRequestMessage request = MessageTestData.SendRequest(
                recipientId, new { clientMessageId, content = "content" }, identityId);

            return await _client.SendAsync(request, TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private static void DisposeAll(IEnumerable<HttpResponseMessage> responses)
    {
        foreach (HttpResponseMessage response in responses)
        {
            response.Dispose();
        }
    }
}
