namespace BigFatFile.Migrator;

/// <param name="Applied">The files this run applied, in order.</param>
/// <param name="UpToDate">The files that were already applied before this run.</param>
public sealed record MigrationReport(IReadOnlyList<MigrationFile> Applied, int UpToDate);
