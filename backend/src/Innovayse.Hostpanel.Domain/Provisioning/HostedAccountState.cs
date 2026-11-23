using Innovayse.Hostpanel.Domain.Provisioning.Enums;

namespace Innovayse.Hostpanel.Domain.Provisioning;

/// <summary>What a panel reports about one hosting account, as far as the platform reconciles it.</summary>
/// <param name="Status">Whether the account is active or suspended on the panel.</param>
/// <param name="Domain">The account's primary domain, or <see langword="null"/> when the panel does not say.</param>
/// <param name="PackageName">The package the account is on, or <see langword="null"/> when the panel does not say.</param>
/// <param name="IpAddress">The IP address the account is served on, or <see langword="null"/> when the panel does not say.</param>
public sealed record HostedAccountState(HostedAccountStatus Status, string? Domain, string? PackageName, string? IpAddress);
