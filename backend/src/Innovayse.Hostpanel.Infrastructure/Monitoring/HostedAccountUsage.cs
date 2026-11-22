namespace Innovayse.Hostpanel.Infrastructure.Monitoring;

/// <summary>
/// Disk and bandwidth usage of one hosting account in megabytes, whichever panel reported it: the one shape the disk usage report
/// reads from the host's own cPanel client and from a panel plugin alike. A limit of zero means unlimited.
/// </summary>
/// <param name="Username">The account's panel username.</param>
/// <param name="Domain">The account's primary domain.</param>
/// <param name="DiskUsedMb">Disk space used.</param>
/// <param name="DiskLimitMb">Disk space allowed; zero for unlimited.</param>
/// <param name="BwUsedMb">Bandwidth used.</param>
/// <param name="BwLimitMb">Bandwidth allowed; zero for unlimited.</param>
public sealed record HostedAccountUsage(string Username, string Domain, long DiskUsedMb, long DiskLimitMb, long BwUsedMb, long BwLimitMb);
