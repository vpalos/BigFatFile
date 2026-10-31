using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BigFatFile.Migrator;

/// <summary>
/// One migration: a file named <c>&lt;prefix&gt;_&lt;number&gt;_&lt;name&gt;.sql</c> and its SQL.
/// </summary>
public sealed partial record MigrationFile
{
    public const string NoTransactionMarker = "-- no-transaction";

    private MigrationFile(string fileName, string prefix, int number, string name, string sql)
    {
        FileName = fileName;
        Prefix = prefix;
        Number = number;
        Name = name;
        Sql = sql.ReplaceLineEndings("\n").TrimStart('﻿');
        Checksum = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Sql)));
        RunsInTransaction = !Sql.StartsWith(NoTransactionMarker, StringComparison.Ordinal);
    }

    public string Prefix { get; }

    public int Number { get; }

    public string Name { get; }

    public string Sql { get; }

    /// <summary>SHA-256 of the SQL with line endings normalised to LF, so a Windows checkout gives the same value.</summary>
    public string Checksum { get; }

    public bool RunsInTransaction { get; }

    public string FileName { get; }

    public static bool IsValidFileName(string fileName) => FileNamePattern().IsMatch(fileName);

    public static MigrationFile Parse(string fileName, string sql)
    {
        var match = FileNamePattern().Match(fileName);
        if (!match.Success)
        {
            throw new MigrationException(
                $"'{fileName}' is not a migration file name. The form is <prefix>_<number>_<name>.sql, in lowercase.");
        }

        return new MigrationFile(
            fileName,
            match.Groups["prefix"].Value,
            int.Parse(match.Groups["number"].Value),
            match.Groups["name"].Value,
            sql);
    }

    public override string ToString() => FileName;

    [GeneratedRegex("^(?<prefix>[a-z][a-z0-9]*)_(?<number>[0-9]{4,9})_(?<name>[a-z0-9_]+)\\.sql$")]
    private static partial Regex FileNamePattern();
}
