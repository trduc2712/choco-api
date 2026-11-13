using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

using Choco.Domain.Posts;
using Choco.IntegrationTests.Notifications;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Choco.IntegrationTests.Logging;

public sealed class RequestLoggingTests : IClassFixture<ChocoApiFactory>, IAsyncLifetime
{
    private readonly ChocoApiFactory _factory;
    private readonly CapturingLoggerProvider _logs = new();
    private readonly HttpClient _client;

    public RequestLoggingTests(ChocoApiFactory factory)
    {
        _factory = factory;
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(_logs));
            builder.ConfigureTestServices(services =>
                services.Replace(ServiceDescriptor.Scoped(_ => DispatchProxy.Create<IPostRepository, ThrowingProxy>())));
        }).CreateClient();
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
    public async Task UnhandledException_ReturnsTheTraceIdAndLogsItWithTheUserAndTheSameTraceId()
    {
        await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        using HttpRequestMessage request = NotificationTestData.CreateRequest(
            HttpMethod.Get, $"/api/v1/posts/{Guid.NewGuid()}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        string traceId = problem.GetProperty("traceId").GetString()!;
        traceId.Should().NotBeNullOrEmpty();
        CapturedLog error = _logs.Entries.Should().ContainSingle(entry => entry.Exception != null).Subject;
        error.Scope["IdentityId"].Should().Be(NotificationTestData.CallerIdentityId);
        error.Scope["TraceId"].Should().Be(traceId);
    }

    [Fact]
    public async Task ControllerError_ReturnsTheSameTraceIdAsTheLogEntriesOfTheRequest()
    {
        await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        using HttpRequestMessage request = NotificationTestData.CreateRequest(
            HttpMethod.Get, $"/api/v1/users/{Guid.NewGuid()}");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        string traceId = problem.GetProperty("traceId").GetString()!;
        traceId.Should().MatchRegex("^[0-9a-f]{32}$");
        CapturedLog http = _logs.Entries.Should().ContainSingle(entry => entry.Category.EndsWith("HttpLoggingMiddleware")).Subject;
        http.Scope["TraceId"].Should().Be(traceId);
    }

    [Fact]
    public async Task AuthenticatedRequest_LogsTheIdentityIdAndNothingElseAboutTheUser()
    {
        await NotificationTestData.AddUserAsync(_factory, NotificationTestData.CallerIdentityId, "username");
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Get, "/api/v1/me");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        CapturedLog http = _logs.Entries.Should().ContainSingle(entry => entry.Category.EndsWith("HttpLoggingMiddleware")).Subject;
        http.Scope["IdentityId"].Should().Be(NotificationTestData.CallerIdentityId);
        _logs.Entries.SelectMany(entry => entry.Scope.Values.Append(entry.Message))
            .Should().NotContain(value => value.Contains("username", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnonymousRequest_LogsNoIdentityId()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/me");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        CapturedLog http = _logs.Entries.Should().ContainSingle(entry => entry.Category.EndsWith("HttpLoggingMiddleware")).Subject;
        http.Scope.Should().NotContainKey("IdentityId");
    }

    public class ThrowingProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            throw new InvalidOperationException("Simulated failure.");
        }
    }
}
