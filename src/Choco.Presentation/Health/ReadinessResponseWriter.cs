using System.Text.Json;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Choco.Presentation.Health;

internal static class ReadinessResponseWriter
{
    public static Task WriteAsync(HttpContext httpContext, HealthReport report)
    {
        httpContext.Response.ContentType = "application/json";

        Dictionary<string, string> checks = report.Entries.ToDictionary(
            entry => entry.Key, entry => entry.Value.Status.ToString());

        return JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            new { status = report.Status.ToString(), checks },
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            httpContext.RequestAborted);
    }
}
