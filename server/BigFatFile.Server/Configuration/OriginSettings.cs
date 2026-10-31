namespace BigFatFile.Server.Configuration;

/// <summary>
/// The server's own settings, read from <c>BFF_*</c> environment variables (or any other
/// configuration source with the same keys).
/// </summary>
internal sealed class OriginSettings
{
    public const string DatabaseKey = "BFF_DATABASE";
    public const string ModulesPathKey = "BFF_MODULES_PATH";
    public const string DevSeedKey = "BFF_DEV_SEED";

    private readonly List<string> errors = [];

    private OriginSettings(IConfiguration configuration, IHostEnvironment environment)
    {
        Database = configuration[DatabaseKey] ?? "";
        if (string.IsNullOrWhiteSpace(Database))
        {
            errors.Add($"{DatabaseKey} is not set. It is the PostgreSQL connection string, for example " +
                       "\"Host=db;Database=bigfatfile;Username=bigfatfile;Password=...\".");
        }

        ModulesPath = ModulesPathFrom(configuration);

        DevSeed = ReadBool(configuration, DevSeedKey);
        if (DevSeed && !environment.IsDevelopment())
        {
            errors.Add($"{DevSeedKey} creates a demo user with a published password, so it works only " +
                       "when ASPNETCORE_ENVIRONMENT is Development.");
        }
    }

    public string Database { get; }

    public string ModulesPath { get; }

    public bool DevSeed { get; }

    public static OriginSettings Read(IConfiguration configuration, IHostEnvironment environment) =>
        new(configuration, environment);

    public static string ModulesPathFrom(IConfiguration configuration) =>
        configuration[ModulesPathKey] is { Length: > 0 } path ? path : "/app/modules";

    /// <summary>Stops startup with a message that names every wrong variable.</summary>
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
