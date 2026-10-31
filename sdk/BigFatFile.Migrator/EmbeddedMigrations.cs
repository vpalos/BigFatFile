using System.Reflection;

namespace BigFatFile.Migrator;

public static class EmbeddedMigrations
{
    /// <summary>
    /// Returns every <c>.sql</c> resource embedded in the assembly as a migration. A <c>.sql</c>
    /// resource with a malformed name is an error rather than skipped, so a typo cannot make
    /// a migration silently disappear.
    /// </summary>
    public static IReadOnlyList<MigrationFile> From(Assembly assembly)
    {
        var files = new List<MigrationFile>();
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            files.Add(MigrationFile.Parse(FileNameOf(resource), reader.ReadToEnd()));
        }

        return files;
    }

    // Resource names join folders with dots or slashes; a migration file name has no dot of its own.
    private static string FileNameOf(string resource)
    {
        var stem = resource[..^".sql".Length];
        var start = stem.LastIndexOfAny(['.', '/', '\\']) + 1;
        return resource[start..];
    }
}
