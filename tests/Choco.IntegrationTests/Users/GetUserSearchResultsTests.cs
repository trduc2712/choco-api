using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Choco.Contract.Abstractions;
using Choco.Contract.Users;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

namespace Choco.IntegrationTests.Users;

public sealed class GetUserSearchResultsTests(ChocoApiFactory factory) : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task NoToken_ReturnsUnauthorized()
    {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/users/search?q=username", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MultipleRanks_ReturnsFirstTenInRelevanceOrderAndContinuesFromCursor()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        User caller = User.Create("identity-id", "username-caller", null, null);
        User exactUsername = User.Create("identity-id-exact-username", "du", null, null);
        User exactName = User.Create("identity-id-exact-name", "username-exact-name", "Đú", null);
        User namePrefix = User.Create("identity-id-name-prefix", "username-name-prefix", "Đu\u0301 Display Name", null);
        User[] usernamePrefixes = Enumerable.Range(1, 8)
            .Select(index => User.Create($"identity-id-prefix-{index}", $"du-username-{index}", null, null))
            .ToArray();
        User contains = User.Create("identity-id-contains", "username-du", null, null);
        await factory.ExecuteDbContextAsync(db =>
        {
            db.AddRange([caller, exactUsername, exactName, namePrefix, contains, .. usernamePrefixes]);
            return db.SaveChangesAsync(cancellationToken);
        });
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow));

        using HttpResponseMessage firstResponse = await client.GetAsync("/api/v1/users/search?q=DU", cancellationToken);
        using HttpResponseMessage suggestionsResponse = await client.GetAsync("/api/v1/users/suggestions?q=DU", cancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        suggestionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        PageResponse<UserSearchItemResponse> firstPage = (await firstResponse.Content
            .ReadFromJsonAsync<PageResponse<UserSearchItemResponse>>(cancellationToken))!;
        UserSearchItemResponse[] suggestions = (await suggestionsResponse.Content
            .ReadFromJsonAsync<UserSearchItemResponse[]>(cancellationToken))!;
        firstPage.Items.Select(item => item.Username).Should().Equal(
            [exactUsername.Username, exactName.Username, .. usernamePrefixes.Select(user => user.Username)]);
        firstPage.Items[1].DisplayName.Should().Be(exactName.DisplayName);
        firstPage.Items.Select(item => item.Id).Should().Equal(
            [exactUsername.Id, exactName.Id, .. usernamePrefixes.Select(user => user.Id)]);
        suggestions.Should().Equal(firstPage.Items);
        firstPage.NextCursor.Should().NotBeNullOrEmpty();

        using HttpResponseMessage secondResponse = await client.GetAsync(
            $"/api/v1/users/search?q=DU&cursor={firstPage.NextCursor}", cancellationToken);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        PageResponse<UserSearchItemResponse> secondPage = (await secondResponse.Content
            .ReadFromJsonAsync<PageResponse<UserSearchItemResponse>>(cancellationToken))!;
        secondPage.Items.Select(item => item.Username).Should().Equal(namePrefix.Username, contains.Username);
        secondPage.NextCursor.Should().BeNull();
    }
}
