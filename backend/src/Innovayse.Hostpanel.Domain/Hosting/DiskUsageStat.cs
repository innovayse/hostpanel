using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Hosting;

/// <summary>Cached disk and bandwidth usage for a hosting account.</summary>
public sealed class DiskUsageStat : BaseEntity
{
    /// <summary>The hosting server hostname.</summary>
    public string ServerName { get; private set; } = string.Empty;

    /// <summary>The client domain / account name.</summary>
    public string Domain { get; private set; } = string.Empty;

    /// <summary>The client name.</summary>
    public string ClientName { get; private set; } = string.Empty;

    /// <summary>Disk usage in MB.</summary>
    public long DiskUsageMb { get; private set; }

    /// <summary>Disk limit in MB. 0 = unlimited.</summary>
    public long DiskLimitMb { get; private set; }

    /// <summary>Bandwidth usage in MB.</summary>
    public long BwUsageMb { get; private set; }

    /// <summary>Bandwidth limit in MB. 0 = unlimited.</summary>
    public long BwLimitMb { get; private set; }

    /// <summary>When this stat was last fetched from the server.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Required by EF Core; use <see cref="Create"/>.</summary>
    private DiskUsageStat() { }

    /// <summary>Creates a new disk usage stat entry.</summary>
    /// <param name="serverName">Hostname of the server the account lives on.</param>
    /// <param name="domain">Primary domain of the hosting account.</param>
    /// <param name="clientName">Display name of the account owner.</param>
    /// <param name="diskUsageMb">Disk space used, in megabytes.</param>
    /// <param name="diskLimitMb">Disk quota in megabytes; 0 means unlimited.</param>
    /// <param name="bwUsageMb">Bandwidth used this period, in megabytes.</param>
    /// <param name="bwLimitMb">Bandwidth quota in megabytes; 0 means unlimited.</param>
    /// <param name="fetchedAt">When the figures were fetched from the server, UTC.</param>
    /// <returns>The new stat entry.</returns>
    public static DiskUsageStat Create(string serverName, string domain, string clientName,
        long diskUsageMb, long diskLimitMb, long bwUsageMb, long bwLimitMb, DateTimeOffset fetchedAt)
        => new()
        {
            ServerName = serverName,
            Domain = domain,
            ClientName = clientName,
            DiskUsageMb = diskUsageMb,
            DiskLimitMb = diskLimitMb,
            BwUsageMb = bwUsageMb,
            BwLimitMb = bwLimitMb,
            UpdatedAt = fetchedAt,
        };

    /// <summary>Records the usage figures fetched from the server and stamps when they were fetched.</summary>
    /// <param name="diskUsageMb">Disk space used, in megabytes.</param>
    /// <param name="diskLimitMb">Disk quota in megabytes; 0 means unlimited.</param>
    /// <param name="bwUsageMb">Bandwidth used this period, in megabytes.</param>
    /// <param name="bwLimitMb">Bandwidth quota in megabytes; 0 means unlimited.</param>
    /// <param name="fetchedAt">When the figures were fetched from the server, UTC.</param>
    public void RecordUsage(long diskUsageMb, long diskLimitMb, long bwUsageMb, long bwLimitMb, DateTimeOffset fetchedAt)
    {
        DiskUsageMb = diskUsageMb;
        DiskLimitMb = diskLimitMb;
        BwUsageMb = bwUsageMb;
        BwLimitMb = bwLimitMb;
        UpdatedAt = fetchedAt;
    }
}
