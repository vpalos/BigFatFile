using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace BigFatFile.Origin.Api.Parameters;

/// <summary>
/// Shows each <see cref="EnumParameter{TEnum}"/> in the spec as its enum, with the allowed values.
/// </summary>
internal static class EnumParameterOpenApi
{
    public static OpenApiOptions AddEnumParameters(this OpenApiOptions options) =>
        options.AddOperationTransformer(DescribeAsync);

    private static async Task DescribeAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        foreach (var description in context.Description.ParameterDescriptions)
        {
            var type = Nullable.GetUnderlyingType(description.Type) ?? description.Type;
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(EnumParameter<>))
            {
                continue;
            }

            if (operation.Parameters?.FirstOrDefault(parameter => parameter.Name == description.Name) is OpenApiParameter parameter)
            {
                parameter.Schema = await context.SchemaReferenceAsync(type.GetGenericArguments()[0], cancellationToken);
            }
        }
    }
}
