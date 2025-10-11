using NJsonSchema;
using NJsonSchema.Generation;

namespace Mireya.Api;

public class FormFileSchemaProcessor : ISchemaProcessor
{
    public void Process(SchemaProcessorContext context)
    {
        if (context.ContextualType != typeof(IFormFile)) return;
        context.Schema.Type = JsonObjectType.String;
        context.Schema.Format = "binary";
    }
}
