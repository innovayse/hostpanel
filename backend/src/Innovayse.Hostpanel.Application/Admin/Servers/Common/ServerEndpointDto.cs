namespace Innovayse.Hostpanel.Application.Admin.Servers.Common;

/// <summary>Where and how a provisioning server's API is reached.</summary>
/// <param name="Hostname">Hostname or IP address used for API access.</param>
/// <param name="IpAddress">Primary public IP address, or null.</param>
/// <param name="AssignedIpAddresses">Additional IPs (newline-separated), or null.</param>
/// <param name="UseSSL">Whether SSL is used for API connections.</param>
/// <param name="AcceptsSelfSignedCertificate">
/// Whether the operator trusts the self signed certificate of the server's API; a plugin reaches the server through a client
/// that accepts it for this server's host name only. Off when omitted.
/// </param>
public sealed record ServerEndpointDto(
    string Hostname,
    string? IpAddress,
    string? AssignedIpAddresses,
    bool UseSSL,
    bool AcceptsSelfSignedCertificate = false);
