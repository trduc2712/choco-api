using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Contract.Abstractions;
using Choco.Contract.Messages;
using Choco.Domain.Conversations;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;
using Choco.IntegrationTests.Notifications;
using Choco.IntegrationTests.Users;

using Microsoft.AspNetCore.Mvc;

namespace Choco.IntegrationTests.Messages;

public sealed class ConversationEndpointsTests(ChocoApiFactory factory) : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string Route = "/api/v1/me/conversations";
    private const string CallerIdentity = "identity-id";
    private const string OtherIdentity = "identity-id-2";

    private readonly HttpClient _client = factory.CreateClient();

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BlockedConversation_StaysVisibleWithDirectionalFlag(bool blockedByCaller)
    {
        (User caller, User other, Conversation conversation) = await SeedConversationAsync();
        await UserBlockTestData.AddBlockAsync(factory, blockedByCaller ? caller : other, blockedByCaller ? other : caller);

        using HttpResponseMessage list = await SendAsync(HttpMethod.Get, Route);
        using HttpResponseMessage lookup = await SendAsync(HttpMethod.Get, $"{Route}/by-user/{other.Id}");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        lookup.StatusCode.Should().Be(HttpStatusCode.OK);
        PageResponse<ConversationResponse> page = await ReadAsync<PageResponse<ConversationResponse>>(list);
        ConversationResponse entry = await ReadAsync<ConversationResponse>(lookup);
        page.Items.Should().ContainSingle().Which.Should().Be(entry);
        entry.Id.Should().Be(conversation.Id);
        entry.OtherUser.Id.Should().Be(other.Id);
        entry.LastMessage!.Sequence.Should().Be(3);
        entry.UnreadCount.Should().Be(3);
        entry.LastReadSequence.Should().Be(0);
        entry.IsBlockedByMe.Should().Be(blockedByCaller);
    }

    [Fact]
    public async Task DeletedParticipant_IsOmittedFromListAndLookup()
    {
        (_, User other, _) = await SeedConversationAsync();
        await NotificationTestData.DeleteUserAsync(factory, other);

        using HttpResponseMessage list = await SendAsync(HttpMethod.Get, Route);
        using HttpResponseMessage lookup = await SendAsync(HttpMethod.Get, $"{Route}/by-user/{other.Id}");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        PageResponse<ConversationResponse> page = await ReadAsync<PageResponse<ConversationResponse>>(list);
        page.Items.Should().BeEmpty();
        await ShouldBeNotFoundAsync(lookup);
    }

    [Fact]
    public async Task ListWithEqualActivity_UsesDescendingIdAcrossPages()
    {
        (User caller, _, Conversation first) = await SeedConversationAsync();
        User other = await NotificationTestData.AddUserAsync(factory, "identity-id-3", "username-3");
        Message second = await MessageTestData.AddMessageAsync(
            factory, caller, other, new Guid("00000000-0000-0000-0000-000000000014"));
        Guid[] expected = new[] { first.Id, second.ConversationId }.OrderDescending().ToArray();

        using HttpResponseMessage firstResponse = await SendAsync(HttpMethod.Get, $"{Route}?limit=1");
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        PageResponse<ConversationResponse> page = await ReadAsync<PageResponse<ConversationResponse>>(firstResponse);
        page.Items.Select(item => item.Id).Should().Equal(expected[0]);
        page.NextCursor.Should().NotBeNull();

        using HttpResponseMessage secondResponse = await SendAsync(HttpMethod.Get, $"{Route}?limit=1&cursor={page.NextCursor}");
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        PageResponse<ConversationResponse> next = await ReadAsync<PageResponse<ConversationResponse>>(secondResponse);
        next.Items.Select(item => item.Id).Should().Equal(expected[1]);
        next.NextCursor.Should().BeNull();
    }

    [Theory]
    [InlineData("", 3, 2)]
    [InlineData("&afterSequence=0", 1, 2)]
    public async Task MessageCursor_ContinuesInTheSameDirection(string query, long first, long second)
    {
        (_, _, Conversation conversation) = await SeedConversationAsync();
        string path = $"{Route}/{conversation.Id}/messages?limit=1";

        using HttpResponseMessage firstResponse = await SendAsync(HttpMethod.Get, path + query);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        PageResponse<MessageResponse> page = await ReadAsync<PageResponse<MessageResponse>>(firstResponse);
        page.Items.Select(message => message.Sequence).Should().Equal(first);
        page.NextCursor.Should().NotBeNull();

        using HttpResponseMessage secondResponse = await SendAsync(HttpMethod.Get, $"{path}&cursor={page.NextCursor}");
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        PageResponse<MessageResponse> next = await ReadAsync<PageResponse<MessageResponse>>(secondResponse);
        next.Items.Select(message => message.Sequence).Should().Equal(second);
    }

    [Fact]
    public async Task CatchUpWithCursor_ReturnsValidationProblem()
    {
        (_, _, Conversation conversation) = await SeedConversationAsync();

        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Get, $"{Route}/{conversation.Id}/messages?cursor=YjI&afterSequence=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails problem = await ReadAsync<ValidationProblemDetails>(response);
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be("cursor");
    }

    [Fact]
    public async Task NegativeReadSequence_ReturnsValidationProblem()
    {
        (_, _, Conversation conversation) = await SeedConversationAsync();

        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Put, $"{Route}/{conversation.Id}/read", new { sequence = -1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails problem = await ReadAsync<ValidationProblemDetails>(response);
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be("sequence");
    }

    [Fact]
    public async Task ReadRequestWithoutToken_ReturnsUnauthorized()
    {
        Guid conversationId = new("00000000-0000-0000-0000-000000000001");
        using JsonContent body = JsonContent.Create(new { sequence = 1 });

        using HttpResponseMessage response = await _client.PutAsync(
            $"{Route}/{conversationId}/read", body, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConcurrentReadRequests_KeepTheHighestPosition()
    {
        (_, User other, Conversation conversation) = await SeedConversationAsync();
        string path = $"{Route}/{conversation.Id}/read";

        Task<HttpResponseMessage> higher = SendAsync(HttpMethod.Put, path, new { sequence = 3 });
        Task<HttpResponseMessage> lower = SendAsync(HttpMethod.Put, path, new { sequence = 1 });
        await Task.WhenAll(higher, lower);
        using HttpResponseMessage higherResponse = await higher;
        using HttpResponseMessage lowerResponse = await lower;

        higherResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        lowerResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, $"{Route}/by-user/{other.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ConversationResponse entry = await ReadAsync<ConversationResponse>(response);
        entry.LastReadSequence.Should().Be(3);
        entry.UnreadCount.Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NonParticipant_ReturnsConversationNotFound(bool write)
    {
        (_, _, Conversation conversation) = await SeedConversationAsync();
        await NotificationTestData.AddUserAsync(factory, "identity-id-3", "username-3");
        string path = $"{Route}/{conversation.Id}/{(write ? "read" : "messages")}";

        using HttpResponseMessage response = await SendAsync(
            write ? HttpMethod.Put : HttpMethod.Get, path, write ? new { sequence = 3 } : null, "identity-id-3");

        await ShouldBeNotFoundAsync(response);
    }

    private async Task<(User Caller, User Other, Conversation Conversation)> SeedConversationAsync()
    {
        User caller = await NotificationTestData.AddUserAsync(factory, CallerIdentity, "username");
        User other = await NotificationTestData.AddUserAsync(factory, OtherIdentity, "username-2");
        DateTimeOffset now = ChocoApiFactory.StartTime;
        Conversation conversation = Conversation.Create(caller.Id, other.Id, now).Value;
        Message first = conversation.SendMessage(other.Id, new Guid("00000000-0000-0000-0000-000000000011"), "content-1", now);
        Message second = conversation.SendMessage(other.Id, new Guid("00000000-0000-0000-0000-000000000012"), "content-2", now);
        Message third = conversation.SendMessage(other.Id, new Guid("00000000-0000-0000-0000-000000000013"), "content-3", now);

        await factory.ExecuteDbContextAsync(db =>
        {
            db.AddRange(conversation, first, second, third);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return (caller, other, conversation);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, object? body = null, string identityId = CallerIdentity)
    {
        using HttpRequestMessage request = new(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: identityId));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken))!;

    private static async Task ShouldBeNotFoundAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails problem = await ReadAsync<ProblemDetails>(response);
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Conversation.NotFound");
    }
}
