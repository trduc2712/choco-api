using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Asp.Versioning.OpenApi.Transformers;

using Choco.Contract.Posts;
using Choco.Presentation.Options;

using Microsoft.Extensions.Options;

namespace Choco.Presentation.OpenApi;

internal static class OpenApiExtensions
{
    public static IApiVersioningBuilder AddApiDocumentation(this IApiVersioningBuilder builder, IConfiguration configuration)
    {
        builder.Services.AddOptions<ApiDocumentationOptions>()
            .Bind(configuration.GetSection(ApiDocumentationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddOpenApi(options =>
        {
            XmlCommentsTransformer contractComments = new(Path.ChangeExtension(typeof(PostResponse).Assembly.Location, ".xml"));
            options.Document.AddSchemaTransformer(contractComments);
            options.Document.AddDocumentTransformer(contractComments);
            options.Document.AddDocumentTransformer((document, context, _) =>
            {
                ApiDocumentationOptions documentation = context.ApplicationServices
                    .GetRequiredService<IOptions<ApiDocumentationOptions>>().Value;

                document.Info.Title = documentation.Title;

                return Task.CompletedTask;
            });
            options.Document.AddDocumentTransformer<BearerSecurityTransformer>();
            options.Document.AddOperationTransformer<BearerSecurityTransformer>();
        });

        return builder;
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

        app.MapOpenApi().WithDocumentPerVersion().AllowAnonymous();

        app.UseSwaggerUI(options =>
        {
            options.DocumentTitle = documentation.Title;
            foreach (ApiVersionDescription version in app.DescribeApiVersions().Reverse())
            {
                options.SwaggerEndpoint($"/openapi/{version.GroupName}.json", $"{documentation.Title} {version.GroupName}");
            }
            options.EnableDeepLinking();
            options.EnableTryItOutByDefault();
            options.DisplayRequestDuration();
            options.UseClerkLogin(app.Services.GetRequiredService<IOptions<ClerkOptions>>().Value);
        });

        return app;
    }
}
