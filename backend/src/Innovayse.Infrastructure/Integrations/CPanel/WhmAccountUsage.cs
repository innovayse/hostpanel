namespace Innovayse.Infrastructure.Integrations.CPanel;

/// <summary>Disk and bandwidth usage for a single WHM account.</summary>
public sealed record WhmAccountUsage(
    string Username,
    string Domain,
    string Owner,
    long DiskUsedMb,
    long DiskLimitMb,
    long BwUsedMb,
    long BwLimitMb);
