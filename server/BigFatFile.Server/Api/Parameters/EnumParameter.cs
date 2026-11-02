using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization;

namespace BigFatFile.Server.Api.Parameters;

/// <summary>
/// A path or query parameter whose value must be one of the JSON names of <typeparamref name="TEnum"/>,
/// which are the values that the spec lists. Never bind a plain enum there: ASP.NET Core reads it by
/// its C# name, and also accepts any number.
/// </summary>
internal readonly record struct EnumParameter<TEnum>(TEnum Value) : IParsable<EnumParameter<TEnum>>
    where TEnum : struct, Enum
{
    private static readonly Dictionary<string, TEnum> ByName = typeof(TEnum)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .ToDictionary(
            field => field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? field.Name,
            field => (TEnum)field.GetValue(null)!,
            StringComparer.Ordinal);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out EnumParameter<TEnum> result)
    {
        var found = ByName.TryGetValue(s ?? "", out var value);
        result = new EnumParameter<TEnum>(value);
        return found;
    }

    public static EnumParameter<TEnum> Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out var result) ? result : throw new FormatException($"'{s}' is not a value of {typeof(TEnum).Name}.");
}
