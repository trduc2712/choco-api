using System.Text.Json.Nodes;

using Choco.Contract.Feedback;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Choco.Presentation.OpenApi;

internal sealed class FeedbackEnumOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Parameters is null
            || context.Description.ActionDescriptor is not ControllerActionDescriptor { ControllerName: "AdminFeedback" })
        {
            return Task.CompletedTask;
        }

        foreach (IOpenApiParameter parameter in operation.Parameters)
        {
            IReadOnlyList<string>? values = parameter.Name switch
            {
                "status" => FeedbackWireValues.Statuses,
                "category" => FeedbackWireValues.Categories,
                _ => null,
            };

            if (values is not null && parameter.Schema is OpenApiSchema schema)
            {
                schema.Enum = values.Select(value => (JsonNode)JsonValue.Create(value)!).ToList();
            }
        }

        return Task.CompletedTask;
    }
}
