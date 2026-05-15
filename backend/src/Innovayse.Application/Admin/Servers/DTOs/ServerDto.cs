namespace Innovayse.Application.Admin.Servers.DTOs;

/// <summary>
/// DTO for a provisioning server returned in list and detail views.
/// </summary>
/// <param name="Id">Unique server identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="Hostname">Hostname or IP used for API access.</param>
/// <param name="IpAddress">Primary public IP address, or null.</param>
/// <param name="AssignedIpAddresses">Additional IPs (newline-separated), or null.</param>
/// <param name="Module">Control panel module identifier string.</param>
/// <param name="Username">API username.</param>
/// <param name="UseSSL">Whether SSL is used for API connections.</param>
/// <param name="MaxAccounts">Account capacity limit, or null for unlimited.</param>
/// <param name="IsDefault">Whether this is the default server for its module type.</param>
/// <param name="IsDisabled">Whether this server is disabled.</param>
/// <param name="MonthlyCost">Monthly cost in USD.</param>
/// <param name="Datacenter">Datacenter or NOC provider name, or null.</param>
/// <param name="ServerStatusAddress">Status page URL, or null.</param>
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
/// <param name="ServerGroupId">ID of the assigned group, or null.</param>
/// <param name="ServerGroupName">Name of the assigned group, or null.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
/// <param name="IsOnline">Whether the last connection test succeeded, or null if never tested.</param>
/// <param name="LastTestedAt">UTC timestamp of the last connection test, or null if never tested.</param>
/// <param name="AccountsCount">Account count reported by the server on last test, or null if unknown.</param>
public record ServerDto(
    int Id,
    string Name,
    string Hostname,
    string? IpAddress,
    string? AssignedIpAddresses,
    string Module,
    string Username,
    bool UseSSL,
    int? MaxAccounts,
    bool IsDefault,
    bool IsDisabled,
    decimal MonthlyCost,
    string? Datacenter,
    string? ServerStatusAddress,
    string? Ns1Hostname, string? Ns1Ip,
    string? Ns2Hostname, string? Ns2Ip,
    string? Ns3Hostname, string? Ns3Ip,
    string? Ns4Hostname, string? Ns4Ip,
    string? Ns5Hostname, string? Ns5Ip,
    int? ServerGroupId,
    string? ServerGroupName,
    DateTimeOffset CreatedAt,
    bool? IsOnline,
    DateTimeOffset? LastTestedAt,
    int? AccountsCount);
