namespace Innovayse.Hostpanel.Application.Admin.Servers.Common;

/// <summary>
/// The few facts about a server that other modules show next to a service (its name and where it
/// answers), so they never read the Servers table themselves. <see cref="ServerOptionDto"/> extends it with the
/// server's state and capacity, so the two never describe the same facts twice.
/// </summary>
/// <param name="Id">The server's id.</param>
/// <param name="Name">The server's display name.</param>
/// <param name="Hostname">The server's host name.</param>
/// <param name="IpAddress">The server's IP address, when one is recorded.</param>
public record ServerBriefDto(int Id, string Name, string Hostname, string? IpAddress);
