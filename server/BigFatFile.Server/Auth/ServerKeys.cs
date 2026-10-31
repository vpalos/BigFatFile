using System.Security.Cryptography;
using Dapper;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace BigFatFile.Server.Auth;

/// <summary>
/// The ES256 key that signs tokens and the AES key that encrypts the ones only the server
/// reads. Both are created on first start and kept in <c>server_keys</c>, so every node
/// uses the same pair.
/// </summary>
internal sealed class ServerKeys(NpgsqlDataSource dataSource)
{
    private const string SigningPurpose = "oidc-signing";
    private const string EncryptionPurpose = "oidc-encryption";

    private SigningCredentials? signing;
    private EncryptingCredentials? encryption;

    public SigningCredentials Signing => signing ?? throw NotLoaded();

    public EncryptingCredentials Encryption => encryption ?? throw NotLoaded();

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var (signingId, signingKey) = await GetOrCreateAsync(connection, SigningPurpose, () =>
        {
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            return ecdsa.ExportPkcs8PrivateKey();
        }, cancellationToken);
        var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(signingKey, out _);
        signing = new SigningCredentials(
            new ECDsaSecurityKey(ecdsa) { KeyId = signingId.ToString("N") }, SecurityAlgorithms.EcdsaSha256);

        var (encryptionId, encryptionKey) = await GetOrCreateAsync(
            connection, EncryptionPurpose, () => RandomNumberGenerator.GetBytes(32), cancellationToken);
        encryption = new EncryptingCredentials(
            new SymmetricSecurityKey(encryptionKey) { KeyId = encryptionId.ToString("N") },
            SecurityAlgorithms.Aes256KW,
            SecurityAlgorithms.Aes256CbcHmacSha512);
    }

    private static async Task<(Guid Id, byte[] Key)> GetOrCreateAsync(
        NpgsqlConnection connection, string purpose, Func<byte[]> create, CancellationToken cancellationToken)
    {
        if (await FindActiveAsync(connection, purpose, cancellationToken) is { } existing)
        {
            return existing;
        }

        // When two nodes start together, both insert and the unique index keeps the first.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO server_keys (id, purpose, key_material)
            VALUES (@Id, @Purpose, @KeyMaterial)
            ON CONFLICT (purpose) WHERE retired_at IS NULL DO NOTHING
            """,
            new { Id = Guid.CreateVersion7(), Purpose = purpose, KeyMaterial = Convert.ToBase64String(create()) },
            cancellationToken: cancellationToken));

        return await FindActiveAsync(connection, purpose, cancellationToken)
            ?? throw new InvalidOperationException($"No active {purpose} key after creating one.");
    }

    private static async Task<(Guid Id, byte[] Key)?> FindActiveAsync(
        NpgsqlConnection connection, string purpose, CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleOrDefaultAsync<(Guid Id, string KeyMaterial)?>(new CommandDefinition(
            "SELECT id, key_material FROM server_keys WHERE purpose = @Purpose AND retired_at IS NULL",
            new { Purpose = purpose },
            cancellationToken: cancellationToken));
        return row is { } found ? (found.Id, Convert.FromBase64String(found.KeyMaterial)) : null;
    }

    private static InvalidOperationException NotLoaded() =>
        new("The server keys are read at startup, before the first request.");
}
