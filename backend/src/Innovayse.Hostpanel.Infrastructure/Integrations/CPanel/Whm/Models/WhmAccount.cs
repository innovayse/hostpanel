namespace Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Models;

/// <summary>A cPanel account, as far as provisioning needs to know it.</summary>
/// <param name="User">The account's username, which is the service's provisioning reference.</param>
/// <param name="Domain">The account's primary domain.</param>
/// <param name="IsSuspended">Whether the account is suspended.</param>
/// <param name="Plan">The package the account is on.</param>
public sealed record WhmAccount(string User, string Domain, bool IsSuspended, string? Plan);
