namespace BigFatFile.Migrator;

public sealed class MigrationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
