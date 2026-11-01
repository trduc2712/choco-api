using System.Text.Encodings.Web;

using Choco.Presentation.Options;

using Swashbuckle.AspNetCore.SwaggerUI;

namespace Choco.Presentation.OpenApi;

internal static class SwaggerUIOptionsExtensions
{
    private const string ClerkJsPath = "/npm/@clerk/clerk-js@6/dist/clerk.browser.js";
    private const string ClerkAuthScriptPath = "/swagger-ui/clerk-auth.js";

    public static void UseClerkLogin(this SwaggerUIOptions options, ClerkOptions clerkOptions)
    {
        if (string.IsNullOrWhiteSpace(clerkOptions.PublishableKey))
        {
            throw new InvalidOperationException(
                $"Missing '{ClerkOptions.SectionName}:{nameof(ClerkOptions.PublishableKey)}', which the Swagger UI needs to sign in.");
        }

        string clerkJsUrl = clerkOptions.Authority.TrimEnd('/') + ClerkJsPath;

        options.HeadContent +=
            $"""<script async crossorigin="anonymous" data-clerk-publishable-key="{HtmlEncoder.Default.Encode(clerkOptions.PublishableKey)}" src="{HtmlEncoder.Default.Encode(clerkJsUrl)}"></script>""";

        options.InjectJavascript(ClerkAuthScriptPath);
        options.ConfigObject.Plugins = ["ClerkAuthPlugin"];
        options.Interceptors.RequestInterceptorFunction = "function (request) { return window.attachClerkToken(request); }";
    }
}
