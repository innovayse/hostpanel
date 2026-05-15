namespace Innovayse.Domain.Servers;

/// <summary>
/// Value object carrying all mutable configuration fields for a <see cref="Server"/>.
/// Used by both <see cref="Server.Create"/> and <see cref="Server.Update"/>.
/// </summary>
/// <param name="Name">Display name.</param>
/// <param name="Hostname">Hostname or IP for API access.</param>
/// <param name="IpAddress">Primary public IP address.</param>
/// <param name="AssignedIpAddresses">Additional IPs (one per line).</param>
/// <param name="Module">Control panel module type.</param>
/// <param name="Username">API username.</param>
/// <param name="Password">API password (null = leave unchanged on update).</param>
/// <param name="ApiToken">API token (null = leave unchanged on update).</param>
/// <param name="AccessHash">Access hash for cPanel/CWP auth (null = leave unchanged on update).</param>
/// <param name="UseSSL">Whether to use SSL when connecting to the API.</param>
/// <param name="MaxAccounts">Account capacity limit, or null for unlimited.</param>
/// <param name="IsDefault">Whether this is the default server for the module.</param>
/// <param name="IsDisabled">Whether new accounts should be blocked from this server.</param>
/// <param name="MonthlyCost">Monthly cost in USD.</param>
/// <param name="Datacenter">Datacenter or NOC provider name.</param>
/// <param name="ServerStatusAddress">URL to the server status page folder.</param>
/// <param name="Ns1Hostname">Primary nameserver hostname.</param>
/// <param name="Ns1Ip">Primary nameserver IP.</param>
/// <param name="Ns2Hostname">Secondary nameserver hostname.</param>
/// <param name="Ns2Ip">Secondary nameserver IP.</param>
/// <param name="Ns3Hostname">Third nameserver hostname.</param>
/// <param name="Ns3Ip">Third nameserver IP.</param>
/// <param name="Ns4Hostname">Fourth nameserver hostname.</param>
/// <param name="Ns4Ip">Fourth nameserver IP.</param>
/// <param name="Ns5Hostname">Fifth nameserver hostname.</param>
/// <param name="Ns5Ip">Fifth nameserver IP.</param>
public record ServerDetails(
    string Name,
    string Hostname,
    string? IpAddress,
    string? AssignedIpAddresses,
    ServerModule Module,
    string Username,
    string? Password,
    string? ApiToken,
    string? AccessHash,
    bool UseSSL,
    int? MaxAccounts,
    bool IsDefault,
    bool IsDisabled,
    decimal MonthlyCost,
    string? Datacenter,
    string? ServerStatusAddress,
    string? Ns1Hostname,
    string? Ns1Ip,
    string? Ns2Hostname,
    string? Ns2Ip,
    string? Ns3Hostname,
    string? Ns3Ip,
    string? Ns4Hostname,
    string? Ns4Ip,
    string? Ns5Hostname,
    string? Ns5Ip);
