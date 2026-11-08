using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Choco.Contract.Users;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

namespace Choco.IntegrationTests.Users;

public sealed class GetUserSuggestionsTests(ChocoApiFactory factory) : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task NoToken_ReturnsUnauthorized()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/users/suggestions?q=u", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("D", "d")]
    [InlineData("%25", "%")]
    public async Task OneCharacter_ReturnsOnlyMatchingActiveUsersOtherThanCaller(string query, string match)
    {
        User caller = User.Create("identity-id", $"username-{match}-caller", null, null);
        User target = User.Create("identity-id-2", $"username-{match}-target", null, "https://example.com/avatar.png");
        User deleted = User.Create("identity-id-3", $"username-{match}-deleted", null, null);
        User other = User.Create("identity-id-4", "username-4", null, null);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await factory.ExecuteDbContextAsync(async db =>
        {
            db.AddRange(caller, target, deleted, other);
            await db.SaveChangesAsync(cancellationToken);
            db.Remove(deleted);
            return await db.SaveChangesAsync(cancellationToken);
        });
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));

        using HttpResponseMessage response = await client.GetAsync($"/api/v1/users/suggestions?q={query}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        UserSearchItemResponse[] body = (await response.Content.ReadFromJsonAsync<UserSearchItemResponse[]>(cancellationToken))!;
        body.Should().Equal(new UserSearchItemResponse(target.Username, target.DisplayName, target.AvatarUrl));
    }
}
