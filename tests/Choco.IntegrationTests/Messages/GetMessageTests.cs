using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Conversations;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;
using Choco.IntegrationTests.Notifications;
using Choco.IntegrationTests.Users;

using Microsoft.AspNetCore.Mvc;

namespace Choco.IntegrationTests.Messages;

public sealed class GetMessageTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownMessageId = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ClientMessageId = new("4b6f2c1a-8d3e-4f5a-9b7c-1e2d3f4a5b6c");

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetMessageTests(ChocoApiFactory factory)
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
        using HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/messages/{UnknownMessageId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("identity-id")]
    [InlineData("identity-id-2")]
    public async Task EitherUserOfTheConversation_ReadsTheMessage(string identityId)
    {
        User sender = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User recipient = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Message message = await MessageTestData.AddMessageAsync(_factory, sender, recipient, ClientMessageId);
        using HttpRequestMessage request = Request(message.Id, identityId);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "id", "conversationId", "sequence", "clientMessageId", "senderId", "content", "createdAt");
        body.GetProperty("id").GetGuid().Should().Be(message.Id);
        body.GetProperty("conversationId").GetGuid().Should().Be(message.ConversationId);
        body.GetProperty("sequence").GetInt64().Should().Be(1);
        body.GetProperty("clientMessageId").GetGuid().Should().Be(ClientMessageId);
        body.GetProperty("senderId").GetGuid().Should().Be(sender.Id);
        body.GetProperty("content").GetString().Should().Be("content");
        body.GetProperty("createdAt").GetDateTimeOffset().Should().Be(ChocoApiFactory.StartTime);
    }

    [Fact]
    public async Task UserOfTheConversation_ReadsTheMessageAfterABlock()
    {
        User sender = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User recipient = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Message message = await MessageTestData.AddMessageAsync(_factory, sender, recipient, ClientMessageId);
        await UserBlockTestData.AddBlockAsync(_factory, recipient, sender);
        using HttpRequestMessage request = Request(message.Id, "identity-id");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UserOutsideTheConversation_ReturnsNotFound()
    {
        User sender = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        User recipient = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        Message message = await MessageTestData.AddMessageAsync(_factory, sender, recipient, ClientMessageId);
        using HttpRequestMessage request = Request(message.Id, "identity-id");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeNotFoundAsync(response);
    }

    [Fact]
    public async Task UnknownMessage_ReturnsNotFound()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = Request(UnknownMessageId, "identity-id");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeNotFoundAsync(response);
    }

    [Fact]
    public async Task EmptyId_ReturnsValidationProblem()
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = Request(Guid.Empty, "identity-id");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be("id");
    }

    private static HttpRequestMessage Request(Guid messageId, string identityId)
    {
        HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/messages/{messageId}");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: identityId));
        return request;
    }

    private static async Task ShouldBeNotFoundAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Message.NotFound");
    }
}
