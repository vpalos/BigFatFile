using BigFatFile.Server.Configuration;
using Dapper;
using Npgsql;

namespace BigFatFile.Server.Database;

/// <summary>
/// Prepares the user in <c>BFF_DATABASE</c>, which requests connect as. It runs as the owner,
/// after the migrations, and stops startup when row-level security would not apply to that user.
/// </summary>
internal sealed class DatabaseRoles(
    OriginSettings settings,
    NpgsqlDataSource app,
    [FromKeyedServices(DatabaseServices.Owner)] NpgsqlDataSource owner,
    ILogger<DatabaseRoles> logger)
{
    public async Task PrepareAsync(CancellationToken cancellationToken)
    {
        var appLogin = new NpgsqlConnectionStringBuilder(settings.Database);
        await using (var connection = await owner.OpenConnectionAsync(cancellationToken))
        {
            await CreateIfMissingAsync(connection, appLogin, cancellationToken);
            await GrantAsync(connection, appLogin.Username!, cancellationToken);
        }

        await using (var connection = await app.OpenConnectionAsync(cancellationToken))
        {
            if (!await connection.ExecuteScalarAsync<bool>("SELECT row_security_active('tenant_members')"))
            {
                throw new InvalidOperationException(
                    $"{OriginSettings.DatabaseKey} connects as {appLogin.Username}, and row-level security does not apply to that user: " +
                    "it is a superuser, it may bypass row-level security, or it owns the tables. " +
                    $"Use a user that does none of these; {OriginSettings.DatabaseOwnerKey} is the one that owns the tables.");
            }
        }
    }

    private async Task CreateIfMissingAsync(NpgsqlConnection connection, NpgsqlConnectionStringBuilder appLogin, CancellationToken cancellationToken)
    {
        if (await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = @Name)", new { Name = appLogin.Username }, cancellationToken: cancellationToken)))
        {
            return;
        }

        var canCreate = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT rolsuper OR rolcreaterole FROM pg_roles WHERE rolname = current_user", cancellationToken: cancellationToken));
        if (!canCreate || string.IsNullOrEmpty(appLogin.Password))
        {
            throw new InvalidOperationException(
                $"The user in {OriginSettings.DatabaseKey}, {appLogin.Username}, does not exist, and the server cannot create it: " +
                $"the user in {OriginSettings.DatabaseOwnerKey} may not create users, or {OriginSettings.DatabaseKey} has no password. " +
                $"Create it with: CREATE ROLE {appLogin.Username} LOGIN PASSWORD '...';");
        }

        var create = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', @Name, @Password)",
            new { Name = appLogin.Username, appLogin.Password },
            cancellationToken: cancellationToken));
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(create!, cancellationToken: cancellationToken));
            logger.LogInformation("Created the database user {Name}, which requests connect as", appLogin.Username);
        }
        catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.DuplicateObject or PostgresErrorCodes.UniqueViolation)
        {
            // Another node created it at the same moment.
        }
    }

    private static async Task GrantAsync(NpgsqlConnection connection, string name, CancellationToken cancellationToken)
    {
        var (role, database) = await connection.QuerySingleAsync<(string Role, string Database)>(new CommandDefinition(
            "SELECT quote_ident(@Name), quote_ident(current_database())", new { Name = name }, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            $"""
            GRANT CONNECT ON DATABASE {database} TO {role};
            GRANT USAGE ON SCHEMA public TO {role};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {role};
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {role};
            REVOKE ALL ON schema_migrations FROM {role};
            """,
            cancellationToken: cancellationToken));
    }
}
