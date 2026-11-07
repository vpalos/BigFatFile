namespace BigFatFile.Origin.Server;

/// <summary>
/// The server's own settings, read from <c>BFF_*</c> environment variables (or any other
/// configuration source with the same keys).
/// </summary>
internal sealed class OriginSettings
{
    public const string DatabaseKey = "BFF_DATABASE";
    public const string DatabaseOwnerKey = "BFF_DATABASE_OWNER";
    public const string ModulesPathKey = "BFF_MODULES_PATH";
    public const string DevelopmentSeedKey = "BFF_DEVELOPMENT_SEED";

    private readonly List<string> errors = [];

    private OriginSettings(IConfiguration configuration, IHostEnvironment environment)
    {
        Database = configuration[DatabaseKey] ?? "";
        if (string.IsNullOrWhiteSpace(Database))
        {
            errors.Add($"{DatabaseKey} is not set. It is the PostgreSQL connection string that the server " +
                       "uses for requests, for example \"Host=db;Database=bigfatfile;Username=bigfatfile_app;Password=...\". " +
                       "Its user must not own the tables.");
        }

        DatabaseOwner = configuration[DatabaseOwnerKey] ?? "";
        if (string.IsNullOrWhiteSpace(DatabaseOwner))
        {
            errors.Add($"{DatabaseOwnerKey} is not set. It is the PostgreSQL connection string of the user that " +
                       "owns the tables, for migrations and the job queue, for example " +
                       "\"Host=db;Database=bigfatfile;Username=bigfatfile;Password=...\".");
        }

        ModulesPath = ModulesPathFrom(configuration);

        DevelopmentSeed = ReadBool(configuration, DevelopmentSeedKey);
        if (DevelopmentSeed && !environment.IsDevelopment())
        {
            errors.Add($"{DevelopmentSeedKey} creates a demo user with a published password, so it works only " +
                       "when ASPNETCORE_ENVIRONMENT is Development.");
        }
    }

    public string Database { get; }

    public string DatabaseOwner { get; }

    public string ModulesPath { get; }

    public bool DevelopmentSeed { get; }

    public static OriginSettings Read(IConfiguration configuration, IHostEnvironment environment) =>
        new(configuration, environment);

    public static string ModulesPathFrom(IConfiguration configuration) =>
        configuration[ModulesPathKey] is { Length: > 0 } path ? path : "/app/modules";

    /// <summary>
    /// Stops startup with a message that names every wrong variable.
    /// </summary>
    public void Validate()
    {
        if (errors.Count > 0)
        {
            throw new InvalidOperationException("The server configuration is wrong:\n- " + string.Join("\n- ", errors));
        }
    }

    private bool ReadBool(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (bool.TryParse(value, out var result))
        {
            return result;
        }

        errors.Add($"{key} is \"{value}\"; use true or false.");
        return false;
    }
}
