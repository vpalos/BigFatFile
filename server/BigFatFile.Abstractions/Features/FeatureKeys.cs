namespace BigFatFile.Abstractions.Features;

/// <summary>
/// The keys reserved for licensed modules.
/// A licence enables some of them; without a licence, all are off.
/// </summary>
public static class FeatureKeys
{
    public const string SsoTenantConfig = "sso.tenant-config";
    public const string SsoSaml = "sso.saml";
    public const string SsoScim = "sso.scim";
    public const string SsoGroups = "sso.groups";
    public const string SsoLdap = "sso.ldap";
    public const string AccessPolicies = "access.policies";

    public const string AuditExtended = "audit.extended";
    public const string GovernanceRetention = "governance.retention";
    public const string GovernanceDlp = "governance.dlp";
    public const string GovernanceDiscovery = "governance.discovery";

    public const string SecurityAtp = "security.atp";
    public const string SecurityRansomware = "security.ransomware";
    public const string SecurityByok = "security.byok";
    public const string OpsDevices = "ops.devices";

    public const string BrandWhitelabel = "brand.whitelabel";
    public const string SharingPolicies = "sharing.policies";
    public const string SharingProtected = "sharing.protected";

    public const string StorageTenantWarehouses = "storage.tenant-warehouses";
    public const string OpsInsights = "ops.insights";

    public const string IntegrationsOffice = "integrations.office";
    public const string IntegrationsExternal = "integrations.external";
    public const string IntegrationsAutomation = "integrations.automation";

    public const string PlatformBilling = "platform.billing";
    public const string PlatformAbuse = "platform.abuse";
    public const string PlatformHosting = "platform.hosting";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        SsoTenantConfig, SsoSaml, SsoScim, SsoGroups, SsoLdap, AccessPolicies,
        AuditExtended, GovernanceRetention, GovernanceDlp, GovernanceDiscovery,
        SecurityAtp, SecurityRansomware, SecurityByok, OpsDevices,
        BrandWhitelabel, SharingPolicies, SharingProtected,
        StorageTenantWarehouses, OpsInsights,
        IntegrationsOffice, IntegrationsExternal, IntegrationsAutomation,
        PlatformBilling, PlatformAbuse, PlatformHosting,
    };
}
