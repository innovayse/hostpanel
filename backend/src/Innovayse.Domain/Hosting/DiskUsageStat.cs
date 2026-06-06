namespace Innovayse.Domain.Hosting;

using Innovayse.Domain.Common;

/// <summary>Cached disk and bandwidth usage for a hosting account.</summary>
public sealed class DiskUsageStat : Entity
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

    private DiskUsageStat() : base(0) { }

    /// <summary>Creates a new disk usage stat entry.</summary>
    public static DiskUsageStat Create(string serverName, string domain, string clientName,
        long diskUsageMb, long diskLimitMb, long bwUsageMb, long bwLimitMb)
        => new()
        {
            ServerName  = serverName,
            Domain      = domain,
            ClientName  = clientName,
            DiskUsageMb = diskUsageMb,
            DiskLimitMb = diskLimitMb,
            BwUsageMb   = bwUsageMb,
            BwLimitMb   = bwLimitMb,
            UpdatedAt   = DateTimeOffset.UtcNow,
        };

    /// <summary>Updates this stat with fresh values.</summary>
    public void Update(long diskUsageMb, long diskLimitMb, long bwUsageMb, long bwLimitMb)
    {
        DiskUsageMb = diskUsageMb;
        DiskLimitMb = diskLimitMb;
        BwUsageMb   = bwUsageMb;
        BwLimitMb   = bwLimitMb;
        UpdatedAt   = DateTimeOffset.UtcNow;
    }
}
