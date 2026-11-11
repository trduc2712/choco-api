using System.Text.Json.Nodes;

using Choco.Presentation.Validation;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Choco.Presentation.OpenApi;

internal sealed class ValidationProblemSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type != typeof(ValidationProblemDetails))
        {
            return Task.CompletedTask;
        }

        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
        schema.Properties["code"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "Always Validation.Failed for a validation problem.",
            Examples = [JsonValue.Create("Validation.Failed")],
        };
        schema.Properties[ValidationProblems.ErrorCodesKey] = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Description = "A stable code for each message in errors, under the same field name and at the same position. "
                + "A code reads Resource.Field.Reason, such as Post.Content.TooLong. Clients translate by code, not by message.",
            AdditionalProperties = new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = new OpenApiSchema { Type = JsonSchemaType.String },
            },
            Examples = [JsonNode.Parse("""{"content":["Post.Content.TooLong"]}""")!],
        };

        return Task.CompletedTask;
    }
}
