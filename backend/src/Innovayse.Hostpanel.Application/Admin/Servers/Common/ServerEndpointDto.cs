namespace Innovayse.Hostpanel.Application.Admin.Servers.Common;

/// <summary>Where and how a provisioning server's API is reached.</summary>
/// <param name="Hostname">Hostname or IP address used for API access.</param>
/// <param name="IpAddress">Primary public IP address, or null.</param>
/// <param name="AssignedIpAddresses">Additional IPs (newline-separated), or null.</param>
/// <param name="UseSSL">Whether SSL is used for API connections.</param>
/// <param name="AcceptsSelfSignedCertificate">
/// Whether the operator trusts the self signed certificate of the server's API; a plugin reaches the server through a client
/// that accepts it for the host the server's API is dialled at only. Off when omitted.
/// </param>
/// <param name="ApiHost">
/// Address the panel's API is dialled at when it does not answer at <paramref name="Hostname"/>,
/// a panel whose API port is reachable only on a private or VPN address, for example. Null or
/// blank keeps the hostname; the hostname still builds the client-facing panel and webmail URLs.
/// </param>
public sealed record ServerEndpointDto(
    string Hostname,
    string? IpAddress,
    string? AssignedIpAddresses,
    bool UseSSL,
    bool AcceptsSelfSignedCertificate = false,
    string? ApiHost = null);
