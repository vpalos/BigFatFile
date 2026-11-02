using OpenIddict.Abstractions;

namespace BigFatFile.Server.Auth;

/// <summary>The scopes an App can ask for. <c>Architecture.API.md</c> section 4 says what each allows.</summary>
internal static class BffScopes
{
    public const string OpenId = OpenIddictConstants.Scopes.OpenId;
    public const string Profile = OpenIddictConstants.Scopes.Profile;
    public const string Email = OpenIddictConstants.Scopes.Email;
    public const string OfflineAccess = OpenIddictConstants.Scopes.OfflineAccess;

    public const string FilesRead = "files.read";
    public const string FilesWrite = "files.write";
    public const string SharesManage = "shares.manage";
    public const string SendsManage = "sends.manage";
    public const string RequestsManage = "requests.manage";
    public const string MembersRead = "members.read";
    public const string MembersManage = "members.manage";
    public const string MetadataRead = "metadata.read";
    public const string MetadataWrite = "metadata.write";
    public const string PermissionsManage = "permissions.manage";
    public const string EventsRead = "events.read";
    public const string WebhooksManage = "webhooks.manage";
    public const string AppsManage = "apps.manage";
    public const string TenantAdmin = "tenant.admin";
    public const string TenantMirror = "tenant.mirror";

    public static IReadOnlyList<string> Standard { get; } = [OpenId, Profile, Email, OfflineAccess];

    public static IReadOnlyDictionary<string, string> Descriptions { get; } = new Dictionary<string, string>
    {
        [OpenId] = "Sign in as a user.",
        [Profile] = "The user's name.",
        [Email] = "The user's email address.",
        [OfflineAccess] = "Refresh tokens, to keep access after the access token expires.",
        [FilesRead] = "List folders, read file metadata, download files, read manifests and chunk grants.",
        [FilesWrite] = "Create, upload, rename, move, trash and restore files; upload sessions.",
        [SharesManage] = "Create and revoke share links on nodes the App may read.",
        [SendsManage] = "Create sends.",
        [RequestsManage] = "Create and manage file requests.",
        [MembersRead] = "List members and groups.",
        [MembersManage] = "Invite and remove members, change roles, manage groups.",
        [MetadataRead] = "Read the App's own metadata namespace.",
        [MetadataWrite] = "Write the App's own metadata namespace.",
        [PermissionsManage] = "Change permission rules on nodes.",
        [EventsRead] = "The change feed and the real-time hub.",
        [WebhooksManage] = "Webhook subscriptions for the App.",
        [AppsManage] = "The developer role: create Apps, rotate keys, see deliveries and usage.",
        [TenantAdmin] = "Tenant settings, warehouses, Replicas, audit and usage.",
        [TenantMirror] = "Every chunk and the whole change feed of the tenant, for mirroring and backup Apps.",
    };

    public static IReadOnlyList<string> Api { get; } = [.. Descriptions.Keys.Except(Standard)];

    public static IEnumerable<string> All => Standard.Concat(Api);

    public static string Permission(string scope) => OpenIddictConstants.Permissions.Prefixes.Scope + scope;
}
