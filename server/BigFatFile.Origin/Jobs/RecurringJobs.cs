using Hangfire;

namespace BigFatFile.Origin.Jobs;

/// <summary>
/// Every recurring job and its schedule. Each job is in its own file next to this one.
/// </summary>
internal static class RecurringJobs
{
    public static void Register(IRecurringJobManager jobs) =>
        jobs.AddOrUpdate<AuditPartitionsJob>("audit-partitions", job => job.RunAsync(CancellationToken.None), Cron.Daily());
}
