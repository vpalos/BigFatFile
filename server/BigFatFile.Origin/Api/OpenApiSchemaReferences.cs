using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace BigFatFile.Origin.Api;

internal static class OpenApiSchemaReferences
{
    /// <summary>
    /// A reference to the component schema of <paramref name="type"/>.
    /// Adds the component when the document does not have it yet.
    /// </summary>
    public static async Task<IOpenApiSchema> SchemaReferenceAsync(this OpenApiOperationTransformerContext context, Type type, CancellationToken cancellationToken)
    {
        var document = context.Document!;
        if (document.Components?.Schemas?.ContainsKey(type.Name) != true)
        {
            document.AddComponent(type.Name, await context.GetOrCreateSchemaAsync(type, null, cancellationToken));
        }

        return new OpenApiSchemaReference(type.Name, document);
    }
}
