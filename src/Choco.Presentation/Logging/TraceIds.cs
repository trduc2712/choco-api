using System.Diagnostics;

using Microsoft.AspNetCore.Mvc;

namespace Choco.Presentation.Logging;

internal static class TraceIds
{
    public const string Key = "traceId";

    public static void Set(ProblemDetails problem, HttpContext httpContext)
    {
        problem.Extensions[Key] = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
    }
}
