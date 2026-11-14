using System.Net;
using System.Text.Json;

using Choco.IntegrationTests.Versioning;

namespace Choco.IntegrationTests.Reports;

public sealed class ReportSearchOpenApiTests : IClassFixture<VersioningApiFactory>
{
    private readonly HttpClient _client;

    public ReportSearchOpenApiTests(VersioningApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/v1/reports", new[] { "q" })]
    [InlineData("/api/v1/admin/reports", new[] { "q", "from", "to" })]
    public async Task OpenApi_DescribesTheSearchAndTimeParameters(string path, string[] described)
    {
        using HttpResponseMessage response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        JsonElement operation = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty("get");

        JsonElement[] parameters = operation.GetProperty("parameters").EnumerateArray().ToArray();
        parameters.Select(parameter => parameter.GetProperty("name").GetString()).Should().Contain(described);
        parameters.Where(parameter => described.Contains(parameter.GetProperty("name").GetString()))
            .Should().OnlyContain(parameter => !string.IsNullOrWhiteSpace(parameter.GetProperty("description").GetString()));
        operation.GetProperty("description").GetString().Should().Contain("q");
        operation.GetProperty("responses").EnumerateObject().Select(status => status.Name)
            .Should().Contain(["200", "400", "401", "403"]);
    }
}
