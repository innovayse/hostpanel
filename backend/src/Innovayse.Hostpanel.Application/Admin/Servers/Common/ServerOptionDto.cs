namespace Innovayse.Hostpanel.Application.Admin.Servers.Common;

/// <summary>
/// What a screen that picks or summarises a server needs to know about it: the brief (name and where it answers), whether it
/// is up and how full it is. No credential, name server or hosting detail travels with it, so the whole set is cheap to send.
/// </summary>
/// <param name="Id">The server's id.</param>
/// <param name="Name">The server's display name.</param>
/// <param name="Hostname">The server's host name.</param>
/// <param name="IpAddress">The server's IP address, when one is recorded.</param>
/// <param name="IsOnline">Whether the last connection test succeeded, or null when the server was never tested.</param>
/// <param name="LastTestedAt">UTC timestamp of the last connection test, or null when the server was never tested.</param>
/// <param name="AccountsCount">The account count the last test reported, or null when unknown.</param>
/// <param name="Limits">The account capacity and whether the server takes new accounts.</param>
public sealed record ServerOptionDto(
    int Id,
    string Name,
    string Hostname,
    string? IpAddress,
    bool? IsOnline,
    DateTimeOffset? LastTestedAt,
    int? AccountsCount,
    ServerLimitsDto Limits) : ServerBriefDto(Id, Name, Hostname, IpAddress);
