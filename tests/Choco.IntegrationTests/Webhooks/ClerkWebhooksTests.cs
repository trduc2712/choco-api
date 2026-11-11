using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Choco.Domain.Roles;
using Choco.Domain.Users;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Choco.IntegrationTests.Webhooks;

public sealed class ClerkWebhooksTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public ClerkWebhooksTests(ChocoApiFactory factory)
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

    [Theory]
    [InlineData("/api/webhooks/clerk")]
    [InlineData("/api/v2/webhooks/clerk")]
    [InlineData("/api/v1.1/webhooks/clerk")]
    [InlineData("/api/vinvalid/webhooks/clerk")]
    public async Task MissingOrUnsupportedVersion_ReturnsNotFoundAndCreatesNoUser(string path)
    {
        using HttpRequestMessage request = ClerkWebhookSigner.CreateSignedRequest(UserCreatedPayload(), "message-id", DateTimeOffset.UtcNow);
        request.RequestUri = new Uri(path, UriKind.Relative);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await GetUsersAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task InvalidSignature_ReturnsUnauthorized()
    {
        using HttpRequestMessage request = ClerkWebhookSigner.CreateSignedRequest(UserCreatedPayload(), "message-id", DateTimeOffset.UtcNow);
        request.Headers.Remove("svix-signature");
        request.Headers.Add("svix-signature", "v1,invalid-signature");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TamperedBody_ReturnsUnauthorizedAndCreatesNoUser()
    {
        using HttpRequestMessage request = ClerkWebhookSigner.CreateSignedRequest(UserCreatedPayload(), "message-id", DateTimeOffset.UtcNow);
        request.Content = new StringContent(UserCreatedPayload(username: "tampered-username"), Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await GetUsersAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("svix-id")]
    [InlineData("svix-timestamp")]
    [InlineData("svix-signature")]
    public async Task MissingHeader_ReturnsUnauthorized(string header)
    {
        using HttpRequestMessage request = ClerkWebhookSigner.CreateSignedRequest(UserCreatedPayload(), "message-id", DateTimeOffset.UtcNow);
        request.Headers.Remove(header);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(-6)]
    [InlineData(6)]
    public async Task TimestampOutsideTolerance_ReturnsUnauthorized(int offsetMinutes)
    {
        using HttpRequestMessage request = ClerkWebhookSigner.CreateSignedRequest(
            UserCreatedPayload(), "message-id", DateTimeOffset.UtcNow.AddMinutes(offsetMinutes));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UserCreatedEvent_CreatesUser()
    {
        string identityId = "identity-id";
        string username = "username";
        string firstName = "first-name";
        string lastName = "last-name";
        string imageUrl = "https://example.com/avatar.png";
        string payload = JsonSerializer.Serialize(new
        {
            type = "user.created",
            data = new
            {
                id = identityId,
                username,
                first_name = firstName,
                last_name = lastName,
                image_url = imageUrl
            }
        });
        using HttpRequestMessage request = ClerkWebhookSigner.CreateSignedRequest(payload, "message-id", DateTimeOffset.UtcNow);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        List<User> users = await GetUsersAsync();
        User user = users.Should().ContainSingle().Subject;
        user.IdentityId.Should().Be(identityId);
        user.Username.Should().Be(username);
        user.DisplayName.Should().Be("first-name last-name");
        user.AvatarUrl.Should().Be(imageUrl);
        user.CreatedAt.Should().Be(ChocoApiFactory.StartTime);
    }

    [Fact]
    public async Task UserCreatedEventWithoutUsername_ReturnsValidationProblemAndCreatesNoUser()
    {
        using HttpRequestMessage request = ClerkWebhookSigner.CreateSignedRequest(
            UserCreatedPayload(username: null), "message-id", DateTimeOffset.UtcNow);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().BeEquivalentTo(["username"]);
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Validation.Failed");
        (await GetUsersAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DuplicateDelivery_CreatesOneUser()
    {
        string payload = UserCreatedPayload();
        using HttpRequestMessage first = ClerkWebhookSigner.CreateSignedRequest(payload, "message-id", DateTimeOffset.UtcNow);
        using HttpRequestMessage second = ClerkWebhookSigner.CreateSignedRequest(payload, "message-id", DateTimeOffset.UtcNow);
        using HttpResponseMessage firstResponse = await _client.SendAsync(first, TestContext.Current.CancellationToken);

        using HttpResponseMessage secondResponse = await _client.SendAsync(second, TestContext.Current.CancellationToken);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetUsersAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task UserCreatedEventForBootstrapAdministrator_CreatesUserWithAdministratorRole()
    {
        Guid adminRoleId = await _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<Role>()
            .Where(role => role.SystemKey == SystemRoles.Admin)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken));
        using HttpRequestMessage request = ClerkWebhookSigner.CreateSignedRequest(
            UserCreatedPayload(identityId: ChocoApiFactory.BootstrapAdminIdentityId), "message-id", DateTimeOffset.UtcNow);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        User user = (await GetUsersAsync()).Should().ContainSingle().Subject;
        user.IdentityId.Should().Be(ChocoApiFactory.BootstrapAdminIdentityId);
        UserRole assignment = user.Roles.Should().ContainSingle().Subject;
        assignment.RoleId.Should().Be(adminRoleId);
        assignment.AssignedBy.Should().BeNull();
        assignment.AssignedAt.Should().Be(ChocoApiFactory.StartTime);
    }

    [Fact]
    public async Task UserCreatedEvent_CreatesUserWithoutRoles()
    {
        using HttpRequestMessage request = ClerkWebhookSigner.CreateSignedRequest(UserCreatedPayload(), "message-id", DateTimeOffset.UtcNow);

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetUsersAsync()).Should().ContainSingle().Which.Roles.Should().BeEmpty();
    }

    private Task<List<User>> GetUsersAsync()
    {
        return _factory.ExecuteDbContextAsync(dbContext => dbContext.Set<User>()
            .AsNoTracking()
            .Include(user => user.Roles)
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    private static string UserCreatedPayload(string? username = "username", string identityId = "identity-id")
    {
        return JsonSerializer.Serialize(new
        {
            type = "user.created",
            data = new
            {
                id = identityId,
                username,
                first_name = "first-name",
                last_name = "last-name",
                image_url = "https://example.com/avatar.png"
            }
        });
    }
}
