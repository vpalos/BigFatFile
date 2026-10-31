namespace BigFatFile.Server.Hosting;

/// <summary>
/// Warns when the server's network does not use BBR, which uploads over lossy links need
/// (Architecture.Deployment.md, section 1). The warning is not shown on non-Linux OSs.
/// </summary>
internal static class CongestionControlCheck
{
    private const string SettingPath = "/proc/sys/net/ipv4/tcp_congestion_control";

    public static void WarnIfNotBbr(ILogger logger)
    {
        if (!File.Exists(SettingPath))
        {
            return;
        }

        var algorithm = File.ReadAllText(SettingPath).Trim();
        if (algorithm != "bbr")
        {
            logger.LogWarning(
                "TCP congestion control is {Algorithm}, not BBR: uploads over lossy links will be slower. " +
                "To fix this, load the tcp_bbr module on the host and set the sysctl in the compose file.",
                algorithm);
        }
    }
}
