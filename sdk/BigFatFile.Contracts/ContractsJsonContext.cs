using System.Text.Json.Serialization;
using BigFatFile.Contracts.Manifests;

namespace BigFatFile.Contracts;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(FileManifest))]
internal sealed partial class ContractsJsonContext : JsonSerializerContext;
