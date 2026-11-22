namespace Innovayse.Hostpanel.Domain.Servers;

/// <summary>
/// Value object carrying all mutable configuration fields for a <see cref="Server"/>.
/// Used by both <see cref="Server.Create"/> and <see cref="Server.Reconfigure"/>.
/// </summary>
/// <param name="Name">Display name.</param>
/// <param name="Hostname">Hostname or IP for API access.</param>
/// <param name="IpAddress">Primary public IP address.</param>
/// <param name="AssignedIpAddresses">Additional IPs (one per line).</param>
/// <param name="Module">Control panel module id; see <see cref="ServerModules"/>.</param>
/// <param name="Username">API username.</param>
/// <param name="Credentials">API secrets (null, empty or whitespace members = leave unchanged on update).</param>
/// <param name="UseSSL">Whether to use SSL when connecting to the API.</param>
/// <param name="MaxAccounts">Account capacity limit, or null for unlimited.</param>
/// <param name="IsDefault">Whether this is the default server for the module.</param>
/// <param name="IsDisabled">Whether new accounts should be blocked from this server.</param>
/// <param name="MonthlyCost">Monthly cost in USD.</param>
/// <param name="Datacenter">Datacenter or NOC provider name.</param>
/// <param name="ServerStatusAddress">URL to the server status page folder.</param>
/// <param name="NameServers">Nameservers the server's accounts are delegated to.</param>
/// <param name="AcceptsSelfSignedCertificate">Whether the operator trusts the self signed certificate of the server's API; off unless set, and only possible with SSL.</param>
public record ServerDetails(
    string Name,
    string Hostname,
    string? IpAddress,
    string? AssignedIpAddresses,
    string Module,
    string Username,
    ServerCredentials Credentials,
    bool UseSSL,
    int? MaxAccounts,
    bool IsDefault,
    bool IsDisabled,
    decimal MonthlyCost,
    string? Datacenter,
    string? ServerStatusAddress,
    ServerNameServers NameServers,
    bool AcceptsSelfSignedCertificate);
