using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Choco.Domain.Posts;
using Choco.IntegrationTests.Notifications;
using Choco.IntegrationTests.Versioning;

namespace Choco.IntegrationTests.Validation;

public sealed class ValidationErrorsTests : IClassFixture<VersioningApiFactory>, IAsyncLifetime
{
    private readonly VersioningApiFactory _factory;
    private readonly HttpClient _client;

    public ValidationErrorsTests(VersioningApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async ValueTask InitializeAsync()
    {
        await _factory.ResetAsync();
        await NotificationTestData.AddUserAsync(_factory, "identity-id", "username");
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData(" ", "Post.Content.Required")]
    [InlineData("TOO_LONG", "Post.Content.TooLong")]
    public async Task InvalidPostContent_ReturnsMessageAndCodeUnderTheJsonFieldName(string content, string expectedCode)
    {
        string sent = content == "TOO_LONG" ? new string('c', Post.ContentMaxLength + 1) : content;

        JsonElement problem = await SendAsync(HttpMethod.Post, "/api/v1/posts", new { content = sent });

        problem.GetProperty("code").GetString().Should().Be("Validation.Failed");
        problem.GetProperty("errors").EnumerateObject().Select(property => property.Name).Should().Equal("content");
        problem.GetProperty("errorCodes").EnumerateObject().Select(property => property.Name).Should().Equal("content");
        Codes(problem, "content").Should().Equal(expectedCode);
        problem.GetProperty("errors").GetProperty("content").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task NestedFieldErrors_UseDottedCamelCaseNamesAndFullPathCodes()
    {
        JsonElement problem = await SendAsync(HttpMethod.Post, "/api/v1/feedback", new
        {
            category = "bug",
            message = "feedback message",
            context = new
            {
                path = "path",
                locale = "en",
                theme = "system",
                userAgent = "user-agent",
                viewport = new { width = 0, height = 720 },
                appVersion = "app-version"
            }
        });

        problem.GetProperty("errorCodes").EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("context.path", "context.viewport.width");
        Codes(problem, "context.path").Should().Equal("Feedback.Context.Path.Invalid");
        Codes(problem, "context.viewport.width").Should().Equal("Feedback.Context.Viewport.Width.OutOfRange");
    }

    [Fact]
    public async Task QueryParameterErrors_UseTheQueryParameterName()
    {
        JsonElement problem = await SendAsync(HttpMethod.Get, "/api/v1/me/posts?limit=0", null);

        problem.GetProperty("errorCodes").EnumerateObject().Select(property => property.Name).Should().Equal("limit");
        Codes(problem, "limit").Should().Equal("Post.Limit.OutOfRange");
    }

    [Fact]
    public async Task ErrorsAndErrorCodes_HaveTheSameFieldsAndLengthsInTheSameOrder()
    {
        JsonElement problem = await SendAsync(HttpMethod.Post, "/api/v1/feedback", new
        {
            category = "unknown",
            message = "short",
            context = new
            {
                path = "path",
                locale = "fr",
                theme = "blue",
                userAgent = new string('u', 600),
                viewport = new { width = 0, height = 0 },
                appVersion = new string('a', 100)
            }
        });

        JsonElement errors = problem.GetProperty("errors");
        JsonElement codes = problem.GetProperty("errorCodes");
        errors.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            codes.EnumerateObject().Select(property => property.Name));
        errors.EnumerateObject().Count().Should().BeGreaterThan(5);
        foreach (JsonProperty field in errors.EnumerateObject())
        {
            codes.GetProperty(field.Name).GetArrayLength().Should().Be(field.Value.GetArrayLength());
        }
    }

    [Fact]
    public async Task EveryCode_ReadsResourceFieldReason()
    {
        JsonElement problem = await SendAsync(HttpMethod.Post, "/api/v1/feedback", new
        {
            category = "unknown",
            message = "short",
            context = new
            {
                path = "path",
                locale = "fr",
                theme = "blue",
                userAgent = new string('u', 600),
                viewport = new { width = 0, height = 0 },
                appVersion = new string('a', 100)
            }
        });

        string[] codes = problem.GetProperty("errorCodes").EnumerateObject()
            .SelectMany(field => field.Value.EnumerateArray().Select(code => code.GetString()!))
            .ToArray();

        codes.Should().OnlyContain(code => code.StartsWith("Feedback.", StringComparison.Ordinal) && code.Split('.').Length >= 3);
        codes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task MalformedJsonBody_ReturnsTheSameShapeWithRequestCodes()
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(HttpMethod.Post, "/api/v1/posts");
        request.Content = new StringContent("{", Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("code").GetString().Should().Be("Validation.Failed");
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
        JsonElement errors = problem.GetProperty("errors");
        JsonElement codes = problem.GetProperty("errorCodes");
        errors.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            codes.EnumerateObject().Select(property => property.Name));
        codes.EnumerateObject().Select(property => property.Name).Should().Equal("body");
        codes.GetProperty("body").EnumerateArray().Select(code => code.GetString()).Should().Equal("Request.Body.Required");
    }

    [Fact]
    public async Task MissingRequiredBodyField_ReturnsRequiredCodeUnderTheJsonFieldName()
    {
        JsonElement problem = await SendAsync(HttpMethod.Post, "/api/v1/feedback", new { });

        problem.GetProperty("code").GetString().Should().Be("Validation.Failed");
        JsonElement codes = problem.GetProperty("errorCodes");
        codes.EnumerateObject().Select(property => property.Name).Should().Contain("category");
        Codes(problem, "category").Should().Equal("Request.Category.Required");
        problem.GetProperty("errors").GetProperty("category").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task IdentityProviderRejection_ReturnsRejectedCodeUnderTheJsonFieldName()
    {
        _factory.IdentityProvider.RespondWith(request => request.Method == HttpMethod.Get
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = "identity-id", username = "username", first_name = "First Name" })
            }
            : new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = JsonContent.Create(new
                {
                    errors = new[] { new { code = "form_param_format_invalid", meta = new { param_name = "first_name" } } }
                })
            });

        JsonElement problem = await SendAsync(
            HttpMethod.Put, "/api/v1/me/profile", new { username = "username-updated", firstName = "First Name", lastName = "Last Name" });

        problem.GetProperty("errors").EnumerateObject().Select(property => property.Name).Should().Equal("firstName");
        Codes(problem, "firstName").Should().Equal("User.FirstName.Rejected");
    }

    [Fact]
    public async Task OpenApiDocument_DescribesTheErrorCodesOfAValidationProblem()
    {
        using HttpResponseMessage response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement document = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        JsonElement properties = document.GetProperty("components").GetProperty("schemas")
            .GetProperty("ValidationProblemDetails").GetProperty("properties");
        properties.EnumerateObject().Select(property => property.Name).Should().Contain(["errors", "errorCodes", "code"]);
        JsonElement errorCodes = properties.GetProperty("errorCodes");
        errorCodes.GetProperty("type").GetString().Should().Be("object");
        errorCodes.GetProperty("additionalProperties").GetProperty("type").GetString().Should().Be("array");
        errorCodes.GetProperty("additionalProperties").GetProperty("items").GetProperty("type").GetString().Should().Be("string");
        errorCodes.GetProperty("description").GetString().Should().Contain("Resource.Field.Reason");
    }

    private static string[] Codes(JsonElement problem, string field)
    {
        return problem.GetProperty("errorCodes").GetProperty(field).EnumerateArray()
            .Select(code => code.GetString()!)
            .ToArray();
    }

    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object? body)
    {
        using HttpRequestMessage request = NotificationTestData.CreateRequest(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using HttpResponseMessage response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }
}
