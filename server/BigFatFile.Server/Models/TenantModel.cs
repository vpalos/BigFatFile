using Dapper;
using Npgsql;

namespace BigFatFile.Server.Models;

/// <summary>The SQL for the <c>tenants</c> table.</summary>
internal static class TenantModel
{
    public sealed record Row(Guid Id, string Name, DateTimeOffset CreatedAt);

    /// <returns>Null when the tenant does not exist, or row-level security hides it.</returns>
    public static async Task<Row?> GetAsync(NpgsqlConnection connection, Guid id, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<(Guid Id, string Name, DateTime CreatedAt)>(new CommandDefinition(
            "SELECT id, name, created_at FROM tenants WHERE id = @Id", new { Id = id }, cancellationToken: cancellationToken));
        return rows.Select(row => new Row(row.Id, row.Name, new DateTimeOffset(row.CreatedAt, TimeSpan.Zero))).SingleOrDefault();
    }

    public static Task CreateAsync(NpgsqlConnection connection, Guid id, string name, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO tenants (id, name) VALUES (@Id, @Name)", new { Id = id, Name = name }, cancellationToken: cancellationToken));
}
