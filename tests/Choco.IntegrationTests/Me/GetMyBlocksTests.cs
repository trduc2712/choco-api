using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Users;
using Choco.IntegrationTests.Notifications;
using Choco.IntegrationTests.Users;

namespace Choco.IntegrationTests.Me;

public sealed class GetMyBlocksTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public GetMyBlocksTests(ChocoApiFactory factory)
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
        using HttpResponseMessage response = await _client.GetAsync("/api/v1/me/blocks", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReturnsOnlyUsersTheCallerBlocked_MostRecentFirst()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User first = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        User second = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        User blocker = await NotificationTestData.AddUserAsync(_factory, "identity-id-4", "username-4");
        DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
        await UserBlockTestData.AddBlockAsync(_factory, caller, first, now);
        await UserBlockTestData.AddBlockAsync(_factory, caller, second, now.AddMinutes(1));
        await UserBlockTestData.AddBlockAsync(_factory, blocker, caller, now.AddMinutes(2));

        JsonElement body = await GetAsync("/api/v1/me/blocks");

        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("items", "nextCursor");
        body.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
        JsonElement[] items = body.GetProperty("items").EnumerateArray().ToArray();
        items.Select(item => item.GetProperty("username").GetString()).Should().Equal("username-3", "username-2");
        items[0].EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("id", "username", "displayName", "avatarUrl", "isFollowedByMe");
        items[0].GetProperty("id").GetGuid().Should().Be(second.Id);
    }

    [Fact]
    public async Task PagesByCursor()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User first = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        User second = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        DateTimeOffset now = _factory.TimeProvider.GetUtcNow();
        await UserBlockTestData.AddBlockAsync(_factory, caller, first, now);
        await UserBlockTestData.AddBlockAsync(_factory, caller, second, now.AddMinutes(1));

        JsonElement firstPage = await GetAsync("/api/v1/me/blocks?limit=1");
        string cursor = firstPage.GetProperty("nextCursor").GetString()!;
        JsonElement secondPage = await GetAsync($"/api/v1/me/blocks?limit=1&cursor={cursor}");

        firstPage.GetProperty("items").EnumerateArray().Single().GetProperty("username").GetString().Should().Be("username-3");
        secondPage.GetProperty("items").EnumerateArray().Single().GetProperty("username").GetString().Should().Be("username-2");
        secondPage.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task DeletedBlockedUser_IsNotListed()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        User blocked = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        await UserBlockTestData.AddBlockAsync(_factory, caller, blocked);
        await NotificationTestData.DeleteUserAsync(_factory, blocked);

        JsonElement body = await GetAsync("/api/v1/me/blocks");

        body.GetProperty("items").EnumerateArray().Should().BeEmpty();
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=51")]
    [InlineData("cursor=not-a-cursor")]
    public async Task InvalidParameters_ReturnBadRequest(string query)
    {
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, $"/api/v1/me/blocks?{query}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<JsonElement> GetAsync(string uri)
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, uri);
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }
}
