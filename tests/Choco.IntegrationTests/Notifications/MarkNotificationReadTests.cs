using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Choco.Domain.Notifications;
using Choco.Domain.Users;

using Microsoft.AspNetCore.Mvc;

namespace Choco.IntegrationTests.Notifications;

public sealed class MarkNotificationReadTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private static readonly Guid UnknownNotificationId = new("00000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset CreatedAt = new(2025, 12, 31, 0, 0, 0, TimeSpan.Zero);

    private readonly ChocoApiFactory _factory;
    private readonly HttpClient _client;

    public MarkNotificationReadTests(ChocoApiFactory factory)
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
        using HttpRequestMessage request = new(HttpMethod.Put, Path(UnknownNotificationId));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnreadNotification_IsMarkedRead()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User actor = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Notification notification = await NotificationTestData.AddNotificationAsync(_factory, caller, actor, CreatedAt);

        HttpStatusCode status = await SendAsync(notification.Id);

        status.Should().Be(HttpStatusCode.NoContent);
        Notification stored = (await NotificationTestData.GetNotificationsAsync(_factory, caller.Id)).Should().ContainSingle().Subject;
        stored.ReadAt.Should().Be(_factory.TimeProvider.GetUtcNow());
    }

    [Fact]
    public async Task RepeatedRequest_KeepsTheFirstReadTime()
    {
        User caller = await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User actor = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        Notification notification = await NotificationTestData.AddNotificationAsync(_factory, caller, actor, CreatedAt);
        DateTimeOffset firstReadAt = _factory.TimeProvider.GetUtcNow();
        await SendAsync(notification.Id);
        _factory.TimeProvider.Advance(TimeSpan.FromHours(1));

        HttpStatusCode status = await SendAsync(notification.Id);

        status.Should().Be(HttpStatusCode.NoContent);
        (await NotificationTestData.GetNotificationsAsync(_factory, caller.Id)).Should().ContainSingle()
            .Which.ReadAt.Should().Be(firstReadAt);
    }

    [Fact]
    public async Task NotificationOfAnotherUser_ReturnsNotFoundAndStaysUnread()
    {
        await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        User other = await NotificationTestData.AddUserAsync(_factory, "identity-id-2", "username-2");
        User actor = await NotificationTestData.AddUserAsync(_factory, "identity-id-3", "username-3");
        Notification notification = await NotificationTestData.AddNotificationAsync(_factory, other, actor, CreatedAt);
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Put, Path(notification.Id));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Notification.NotFound");
        (await NotificationTestData.GetNotificationsAsync(_factory, other.Id)).Should().ContainSingle()
            .Which.ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task UnknownNotification_ReturnsNotFound()
    {
        await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Put, Path(UnknownNotificationId));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Notification.NotFound");
    }

    [Fact]
    public async Task EmptyNotificationId_ReturnsValidationProblem()
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Put, Path(Guid.Empty));

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ValidationProblemDetails problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Errors.Keys.Should().ContainSingle().Which.Should().Be("notificationId");
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be("Validation.Failed");
    }

    private async Task<HttpStatusCode> SendAsync(Guid notificationId)
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Put, Path(notificationId));
        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        return response.StatusCode;
    }

    private static string Path(Guid notificationId)
    {
        return $"/api/v1/me/notifications/{notificationId}/read";
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!;
        problem.Status.Should().Be((int)status);
        problem.Extensions["code"].Should().BeOfType<JsonElement>().Which.GetString().Should().Be(code);
    }
}
