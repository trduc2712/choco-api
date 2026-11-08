using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Contract.Users;
using Choco.Domain.Users;
using Choco.IntegrationTests.Authentication;

using Microsoft.AspNetCore.Mvc;

namespace Choco.IntegrationTests.Me;

public sealed class EditMyProfileTests(ChocoApiFactory factory) : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private const string ProfilePath = "/api/v1/me/profile";
    private const string AvatarPath = ProfilePath + "/avatar";
    private const string AvatarUrl = "https://example.com/avatar.png";

    private readonly HttpClient _client = factory.CreateClient();

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task SavesNameAndUsernameForTheCallerAndOtherReaders()
    {
        User caller = await AddUsersAsync();
        JsonElement sent = default;
        factory.IdentityProvider.RespondWithAsync(async (request, cancellationToken) =>
        {
            string json = await request.Content!.ReadAsStringAsync(cancellationToken);
            sent = JsonSerializer.Deserialize<JsonElement>(json);
            return ProviderProfile("username-updated", "First Name", "Last Name", AvatarUrl);
        });
        using HttpRequestMessage request = Request(HttpMethod.Put, ProfilePath);
        request.Content = ProfileBody();

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        UserProfileResponse expected = new(caller.Id, "username-updated", "First Name Last Name", AvatarUrl);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<UserProfileResponse>(TestContext.Current.CancellationToken)).Should().Be(expected);
        (await ReadProfileAsync(ProfilePath)).Should().Be(expected);
        (await ReadProfileAsync($"/api/v1/users/{caller.Id}", "identity-id-2")).Should().Be(expected);
        HttpRequestMessage outgoing = factory.IdentityProvider.Requests.Should().ContainSingle().Subject;
        outgoing.Method.Should().Be(HttpMethod.Patch);
        outgoing.RequestUri.Should().Be(new Uri(ChocoApiFactory.IdentityProviderUrl, "users/identity-id"));
        sent.GetProperty("username").GetString().Should().Be("username-updated");
        sent.GetProperty("first_name").GetString().Should().Be("First Name");
        sent.GetProperty("last_name").GetString().Should().Be("Last Name");
    }

    [Fact]
    public async Task InvalidProfileReturnsValidationWithoutCallingProvider()
    {
        await AddUsersAsync();
        using HttpRequestMessage request = Request(HttpMethod.Put, ProfilePath);
        request.Content = JsonContent.Create(new { username = "username-updated", firstName = " " });

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be("FirstName");
        ((JsonElement)problem.Extensions["code"]!).GetString().Should().Be("Validation.Failed");
        (await ReadProfileAsync(ProfilePath)).DisplayName.Should().Be("Display Name");
        factory.IdentityProvider.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("username-2", false)]
    [InlineData("username-updated", true)]
    public async Task UnavailableUsernameLeavesProfileUnchanged(string username, bool rejectedByProvider)
    {
        User caller = await AddUsersAsync();
        factory.IdentityProvider.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = JsonContent.Create(new { errors = new[] { new { code = "form_identifier_exists", meta = new { param_name = "username" } } } })
        });
        using HttpRequestMessage request = Request(HttpMethod.Put, ProfilePath);
        request.Content = ProfileBody(username);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        ((JsonElement)problem.Extensions["code"]!).GetString().Should().Be("User.UsernameTaken");
        (await ReadProfileAsync(ProfilePath)).Should().Be(new UserProfileResponse(caller.Id, "username", "Display Name", AvatarUrl));
        factory.IdentityProvider.Requests.Should().HaveCount(rejectedByProvider ? 1 : 0);
    }

    [Fact]
    public async Task UploadedAvatarIsVisibleOnThePublicProfile()
    {
        const string newAvatarUrl = "https://example.com/avatar-updated.png";
        User caller = await AddUsersAsync();
        byte[] image = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
        string? uploadField = null;
        byte[]? uploadedImage = null;
        factory.IdentityProvider.RespondWithAsync(async (request, cancellationToken) =>
        {
            HttpContent file = ((MultipartFormDataContent)request.Content!).Single();
            uploadField = file.Headers.ContentDisposition?.Name?.Trim('"');
            uploadedImage = await file.ReadAsByteArrayAsync(cancellationToken);
            return ProviderProfile("username", "Display Name", null, newAvatarUrl);
        });
        using HttpRequestMessage request = Request(HttpMethod.Post, AvatarPath);
        request.Content = ImageBody(image);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        UserProfileResponse expected = new(caller.Id, "username", "Display Name", newAvatarUrl);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<UserProfileResponse>(TestContext.Current.CancellationToken)).Should().Be(expected);
        (await ReadProfileAsync($"/api/v1/users/{caller.Id}", "identity-id-2")).Should().Be(expected);
        HttpRequestMessage outgoing = factory.IdentityProvider.Requests.Should().ContainSingle().Subject;
        outgoing.Method.Should().Be(HttpMethod.Post);
        outgoing.RequestUri.Should().Be(new Uri(ChocoApiFactory.IdentityProviderUrl, "users/identity-id/profile_image"));
        uploadField.Should().Be("file");
        uploadedImage.Should().Equal(image);
    }

    [Fact]
    public async Task RejectedImagePreservesCurrentAvatar()
    {
        await AddUsersAsync();
        factory.IdentityProvider.RespondWith(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { errors = new[] { new { code = "request_body_invalid" } } })
        });
        using HttpRequestMessage request = Request(HttpMethod.Post, AvatarPath);
        request.Content = ImageBody([1, 2, 3]);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be("Image");
        (await ReadProfileAsync(ProfilePath)).AvatarUrl.Should().Be(AvatarUrl);
        factory.IdentityProvider.Requests.Should().ContainSingle();
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task RemovedAvatarIsClearedLocallyIncludingWhenAlreadyAbsentAtProvider(HttpStatusCode providerStatus)
    {
        User caller = await AddUsersAsync();
        factory.IdentityProvider.RespondWith(_ => new HttpResponseMessage(providerStatus)
        {
            Content = JsonContent.Create(new { errors = new[] { new { code = "image_not_found" } } })
        });
        using HttpRequestMessage request = Request(HttpMethod.Delete, AvatarPath);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        UserProfileResponse expected = new(caller.Id, "username", "Display Name", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<UserProfileResponse>(TestContext.Current.CancellationToken)).Should().Be(expected);
        (await ReadProfileAsync($"/api/v1/users/{caller.Id}", "identity-id-2")).Should().Be(expected);
        HttpRequestMessage outgoing = factory.IdentityProvider.Requests.Should().ContainSingle().Subject;
        outgoing.Method.Should().Be(HttpMethod.Delete);
        outgoing.RequestUri.Should().Be(new Uri(ChocoApiFactory.IdentityProviderUrl, "users/identity-id/profile_image"));
    }

    [Theory]
    [InlineData("PUT", ProfilePath)]
    [InlineData("POST", AvatarPath)]
    [InlineData("DELETE", AvatarPath)]
    public async Task NoTokenRejectsProfileWrite(string method, string path)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), path)
        {
            Content = method == "POST" ? ImageBody([1, 2, 3]) : null
        };

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        factory.IdentityProvider.Requests.Should().BeEmpty();
    }

    private async Task<User> AddUsersAsync()
    {
        User caller = User.Create("identity-id", "username", "Display Name", AvatarUrl);
        User other = User.Create("identity-id-2", "username-2", "Display Name 2", null);
        await factory.ExecuteDbContextAsync(db =>
        {
            db.AddRange(caller, other);
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return caller;
    }

    private async Task<UserProfileResponse> ReadProfileAsync(string path, string identityId = "identity-id")
    {
        using HttpRequestMessage request = Request(HttpMethod.Get, path, identityId);
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<UserProfileResponse>(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("The profile response was empty.");
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string identityId = "identity-id")
    {
        HttpRequestMessage request = new(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Create(DateTimeOffset.UtcNow, subject: identityId));
        return request;
    }

    private static JsonContent ProfileBody(string username = "username-updated") =>
        JsonContent.Create(new { username, firstName = "First Name", lastName = "Last Name" });

    private static MultipartFormDataContent ImageBody(byte[] image)
    {
        MultipartFormDataContent body = new();
        body.Add(new ByteArrayContent(image), "image", "avatar.png");
        return body;
    }

    private static HttpResponseMessage ProviderProfile(string username, string firstName, string? lastName, string avatarUrl) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = "identity-id",
                username,
                first_name = firstName,
                last_name = lastName,
                image_url = avatarUrl
            })
        };
}
