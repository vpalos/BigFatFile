using OpenIddict.Abstractions;

namespace BigFatFile.Server.Auth;

/// <summary>The scopes an App can ask for. <c>Architecture.API.md</c> section 4 says what each allows.</summary>
internal static class BffScopes
{
    public const string OpenId = OpenIddictConstants.Scopes.OpenId;
    public const string Profile = OpenIddictConstants.Scopes.Profile;
    public const string Email = OpenIddictConstants.Scopes.Email;
    public const string OfflineAccess = OpenIddictConstants.Scopes.OfflineAccess;

    public static IReadOnlyList<string> Standard { get; } = [OpenId, Profile, Email, OfflineAccess];

    public static IReadOnlyDictionary<string, string> Descriptions { get; } = new Dictionary<string, string>
    {
        [OpenId] = "Sign in as a user.",
        [Profile] = "The user's name.",
        [Email] = "The user's email address.",
        [OfflineAccess] = "Refresh tokens, to keep access after the access token expires.",
        ["files.read"] = "List folders, read file metadata, download files, read manifests and chunk grants.",
        ["files.write"] = "Create, upload, rename, move, trash and restore files; upload sessions.",
        ["shares.manage"] = "Create and revoke share links on nodes the App may read.",
        ["sends.manage"] = "Create sends.",
        ["requests.manage"] = "Create and manage file requests.",
        ["members.read"] = "List members and groups.",
        ["members.manage"] = "Invite and remove members, change roles, manage groups.",
        ["metadata.read"] = "Read the App's own metadata namespace.",
        ["metadata.write"] = "Write the App's own metadata namespace.",
        ["permissions.manage"] = "Change permission rules on nodes.",
        ["events.read"] = "The change feed and the real-time hub.",
        ["webhooks.manage"] = "Webhook subscriptions for the App.",
        ["apps.manage"] = "The developer role: create Apps, rotate keys, see deliveries and usage.",
        ["tenant.admin"] = "Tenant settings, warehouses, Replicas, audit and usage.",
        ["tenant.mirror"] = "Every chunk and the whole change feed of the tenant, for mirroring and backup Apps.",
    };

    public static IReadOnlyList<string> Api { get; } = [.. Descriptions.Keys.Except(Standard)];

    public static IEnumerable<string> All => Standard.Concat(Api);

    public static string Permission(string scope) => OpenIddictConstants.Permissions.Prefixes.Scope + scope;
}
