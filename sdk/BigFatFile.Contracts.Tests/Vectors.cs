using System.Text.Json;

namespace BigFatFile.Contracts.Tests;

internal static class Vectors
{
    public static JsonElement Load(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "vectors", file);
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    }

    public static JsonElement Find(string file, string name) =>
        Load(file).GetProperty("vectors").EnumerateArray().Single(v => v.GetProperty("name").GetString() == name);

    public static TheoryData<string> Names(string file)
    {
        var names = new TheoryData<string>();
        foreach (var vector in Load(file).GetProperty("vectors").EnumerateArray())
        {
            names.Add(vector.GetProperty("name").GetString()!);
        }

        return names;
    }
}
