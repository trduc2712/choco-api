using Choco.Presentation.Options;

using Microsoft.Extensions.Options;

namespace Choco.Presentation.OpenApi;

internal static class OpenApiExtensions
{
    private const string DocumentUrl = "/openapi/v1.json";

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ApiDocumentationOptions>()
            .Bind(configuration.GetSection(ApiDocumentationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, _) =>
            {
                ApiDocumentationOptions documentation = context.ApplicationServices
                    .GetRequiredService<IOptions<ApiDocumentationOptions>>().Value;

                document.Info.Title = documentation.Title;

                return Task.CompletedTask;
            });
            options.AddDocumentTransformer<BearerSecurityTransformer>();
            options.AddOperationTransformer<BearerSecurityTransformer>();
        });

        return services;
    }

    public static WebApplication UseApiDocumentation(this WebApplication app)
    {
        ApiDocumentationOptions documentation = app.Services.GetRequiredService<IOptions<ApiDocumentationOptions>>().Value;

        if (!documentation.Enabled)
        {
            return app;
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache"
        });

        app.MapOpenApi().AllowAnonymous();

        app.UseSwaggerUI(options =>
        {
            options.DocumentTitle = documentation.Title;
            options.SwaggerEndpoint(DocumentUrl, documentation.Title);
            options.EnableDeepLinking();
            options.EnableTryItOutByDefault();
            options.DisplayRequestDuration();
            options.UseClerkLogin(app.Services.GetRequiredService<IOptions<ClerkOptions>>().Value);
        });

        return app;
    }
}
