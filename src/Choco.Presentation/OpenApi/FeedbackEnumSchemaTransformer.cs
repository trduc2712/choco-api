using System.Text.Json.Nodes;

using Choco.Contract.Feedback;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Choco.Presentation.OpenApi;

internal sealed class FeedbackEnumSchemaTransformer : IOpenApiSchemaTransformer
{
    private static readonly Dictionary<Type, (string Property, IReadOnlyList<string> Values)[]> EnumProperties = new()
    {
        [typeof(CreateFeedbackCommand)] = [("category", FeedbackWireValues.Categories)],
        [typeof(CreateFeedbackContextDto)] = [("locale", FeedbackWireValues.Locales), ("theme", FeedbackWireValues.Themes)],
        [typeof(FeedbackContextDto)] = [("locale", FeedbackWireValues.Locales), ("theme", FeedbackWireValues.Themes)],
        [typeof(FeedbackResponse)] = [("category", FeedbackWireValues.Categories), ("status", FeedbackWireValues.Statuses)],
        [typeof(FeedbackSummaryResponse)] = [("category", FeedbackWireValues.Categories), ("status", FeedbackWireValues.Statuses)],
        [typeof(MyFeedbackResponse)] = [("category", FeedbackWireValues.Categories), ("status", FeedbackWireValues.Statuses)],
        [typeof(UpdateFeedbackStatusCommand)] = [("status", FeedbackWireValues.Statuses)],
    };

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (schema.Properties is null || !EnumProperties.TryGetValue(context.JsonTypeInfo.Type, out var fields))
        {
            return Task.CompletedTask;
        }

        foreach ((string name, IReadOnlyList<string> values) in fields)
        {
            if (schema.Properties.TryGetValue(name, out IOpenApiSchema? property) && property is OpenApiSchema concrete)
            {
                concrete.Enum = values.Select(value => (JsonNode)JsonValue.Create(value)!).ToList();
            }
        }

        return Task.CompletedTask;
    }
}
