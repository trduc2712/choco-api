using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Conversations;
using Choco.Domain.Users;
using Choco.IntegrationTests.Notifications;
using Choco.IntegrationTests.Users;

using Microsoft.AspNetCore.Mvc;

namespace Choco.IntegrationTests.Messages;

public sealed class SendMessageTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownUserId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ClientMessageId = new("4b6f2c1a-8d3e-4f5a-9b7c-1e2d3f4a5b6c");
    private static readonly Guid OtherClientMessageId = new("5c7a3d2b-9e4f-4a6b-8c8d-2f3e4a5b6c7d");

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public SendMessageTests(ChocoApiFactory factory)
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
    public async Task NoToken_ReturnsUnauthorized()
    {
        using HttpRequestMessage request = new(HttpMethod.Post, $"/api/v1/users/{UnknownUserId}/messages")
        {
            Content = JsonContent.Create(new { clientMessageId = ClientMessageId, content = "content" })
        };

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FirstMessage_StartsTheConversationAndReturnsCreated()
    {
        User sender = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User recipient = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        using HttpRequestMessage request = MessageTestData.SendRequest(
            recipient.Id, new { clientMessageId = ClientMessageId, content = "  content  " });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Conversation conversation = (await MessageTestData.GetConversationsAsync(_factory)).Should().ContainSingle().Subject;
        Message message = (await MessageTestData.GetMessagesAsync(_factory)).Should().ContainSingle().Subject;
        response.Headers.Location.Should().Be(new Uri(_client.BaseAddress!, $"/api/v1/messages/{message.Id}"));
        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "id", "conversationId", "sequence", "clientMessageId", "senderId", "content", "createdAt");
        body.GetProperty("id").GetGuid().Should().Be(message.Id);
        body.GetProperty("conversationId").GetGuid().Should().Be(conversation.Id);
        body.GetProperty("sequence").GetInt64().Should().Be(1);
        body.GetProperty("clientMessageId").GetGuid().Should().Be(ClientMessageId);
        body.GetProperty("senderId").GetGuid().Should().Be(sender.Id);
        body.GetProperty("content").GetString().Should().Be("content");
        body.GetProperty("createdAt").GetDateTimeOffset().Should().Be(ChocoApiFactory.StartTime);
        conversation.IsParticipant(sender.Id).Should().BeTrue();
        conversation.IsParticipant(recipient.Id).Should().BeTrue();
        conversation.LastMessageSequence.Should().Be(1);
        conversation.LastActivityAt.Should().Be(ChocoApiFactory.StartTime);
        conversation.UnreadCountFor(sender.Id).Should().Be(0);
        conversation.UnreadCountFor(recipient.Id).Should().Be(1);
        message.ConversationId.Should().Be(conversation.Id);
        message.Sequence.Should().Be(1);
        message.SenderId.Should().Be(sender.Id);
        message.ClientMessageId.Should().Be(ClientMessageId);
        message.Content.Should().Be("content");
        message.SentAt.Should().Be(ChocoApiFactory.StartTime);
    }

    [Fact]
    public async Task Reply_JoinsTheConversationWithTheNextSequence()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User other = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Message first = await MessageTestData.AddMessageAsync(_factory, other, caller, OtherClientMessageId);
        _factory.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        using HttpRequestMessage request = MessageTestData.SendRequest(
            other.Id, new { clientMessageId = ClientMessageId, content = "content-2" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("conversationId").GetGuid().Should().Be(first.ConversationId);
        body.GetProperty("sequence").GetInt64().Should().Be(2);
        Conversation conversation = (await MessageTestData.GetConversationsAsync(_factory)).Should().ContainSingle().Subject;
        conversation.LastMessageSequence.Should().Be(2);
        conversation.LastActivityAt.Should().Be(ChocoApiFactory.StartTime.AddMinutes(1));
        conversation.UnreadCountFor(caller.Id).Should().Be(0);
        conversation.UnreadCountFor(other.Id).Should().Be(1);
        (await MessageTestData.GetMessagesAsync(_factory)).Select(message => message.Sequence).Should().Equal(1, 2);
    }

    [Fact]
    public async Task SameClientMessageIdAgain_ReturnsTheSavedMessageAndAddsNothing()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User recipient = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        using HttpRequestMessage first = MessageTestData.SendRequest(
            recipient.Id, new { clientMessageId = ClientMessageId, content = "content" });
        using HttpRequestMessage retry = MessageTestData.SendRequest(
            recipient.Id, new { clientMessageId = ClientMessageId, content = "content-retried" });

        using HttpResponseMessage firstResponse = await _client.SendAsync(first, TestContext.Current.CancellationToken);
        using HttpResponseMessage retryResponse = await _client.SendAsync(retry, TestContext.Current.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        retryResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        string firstBody = await firstResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        string retryBody = await retryResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        retryBody.Should().Be(firstBody);
        retryResponse.Headers.Location.Should().Be(firstResponse.Headers.Location);
        Message message = (await MessageTestData.GetMessagesAsync(_factory)).Should().ContainSingle().Subject;
        message.Content.Should().Be("content");
        (await MessageTestData.GetConversationsAsync(_factory)).Should().ContainSingle()
            .Which.LastMessageSequence.Should().Be(1);
    }

    [Fact]
    public async Task ClientMessageIdUsedByTheOtherUser_AddsASeparateMessage()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User other = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        await MessageTestData.AddMessageAsync(_factory, other, caller, ClientMessageId);
        using HttpRequestMessage request = MessageTestData.SendRequest(
            other.Id, new { clientMessageId = ClientMessageId, content = "content-2" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        List<Message> messages = await MessageTestData.GetMessagesAsync(_factory);
        messages.Select(message => message.SenderId).Should().Equal(other.Id, caller.Id);
        messages.Select(message => message.Sequence).Should().Equal(1, 2);
    }

    [Fact]
    public async Task ContentOverMaxLength_ReturnsValidationProblemAndAddsNothing()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User recipient = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        using HttpRequestMessage request = MessageTestData.SendRequest(
            recipient.Id, new { clientMessageId = ClientMessageId, content = new string('c', Message.ContentMaxLength + 1) });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeValidationProblemAsync(response, "content");
        (await MessageTestData.GetConversationsAsync(_factory)).Should().BeEmpty();
        (await MessageTestData.GetMessagesAsync(_factory)).Should().BeEmpty();
    }

    [Fact]
    public async Task MissingClientMessageId_ReturnsValidationProblem()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User recipient = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        using HttpRequestMessage request = MessageTestData.SendRequest(recipient.Id, new { content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeValidationProblemAsync(response, "clientMessageId");
    }

    [Fact]
    public async Task OwnAccount_ReturnsBadRequestAndAddsNothing()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = MessageTestData.SendRequest(
            caller.Id, new { clientMessageId = ClientMessageId, content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "Conversation.SelfConversation");
        (await MessageTestData.GetConversationsAsync(_factory)).Should().BeEmpty();
    }

    [Fact]
    public async Task UnknownRecipient_ReturnsNotFoundAndAddsNothing()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = MessageTestData.SendRequest(
            UnknownUserId, new { clientMessageId = ClientMessageId, content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "User.NotFound");
        (await MessageTestData.GetConversationsAsync(_factory)).Should().BeEmpty();
    }

    [Fact]
    public async Task DeletedRecipient_ReturnsNotFoundAndAddsNothing()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User recipient = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        await NotificationTestData.DeleteUserAsync(_factory, recipient);
        using HttpRequestMessage request = MessageTestData.SendRequest(
            recipient.Id, new { clientMessageId = ClientMessageId, content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "User.NotFound");
        (await MessageTestData.GetConversationsAsync(_factory)).Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BlockBetweenTheUsers_ReturnsForbiddenAndAddsNothing(bool callerBlocked)
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User recipient = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        await UserBlockTestData.AddBlockAsync(
            _factory, callerBlocked ? caller : recipient, callerBlocked ? recipient : caller);
        using HttpRequestMessage request = MessageTestData.SendRequest(
            recipient.Id, new { clientMessageId = ClientMessageId, content = "content" });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "UserBlock.Blocked");
        (await MessageTestData.GetConversationsAsync(_factory)).Should().BeEmpty();
    }

    private static async Task ShouldBeValidationProblemAsync(HttpResponseMessage response, string field)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be(field);
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Validation.Failed");
    }

    private static async Task ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Status.Should().Be((int)status);
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be(code);
    }
}
