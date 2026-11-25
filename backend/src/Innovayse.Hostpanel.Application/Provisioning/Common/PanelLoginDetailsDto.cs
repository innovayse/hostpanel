namespace Innovayse.Hostpanel.Application.Provisioning.Common;

/// <summary>
/// How a person signs in to the control panel of a hosting account by hand: the panel's own sign in page, the user name and the
/// server. It is what the Open panel button shows when the panel cannot issue a single sign on link. It never carries a password:
/// the person already holds it, and the host never reveals a stored one on this route.
/// </summary>
/// <param name="Username">The user name the account signs in with.</param>
/// <param name="Domain">The primary domain of the account; empty when the panel reports none.</param>
/// <param name="ServerAddress">The address of the server the account lives on.</param>
/// <param name="LoginUrl">The address of the panel's sign in page for this account, opened by the person in a new tab.</param>
public sealed record PanelLoginDetailsDto(string Username, string Domain, string ServerAddress, string LoginUrl);
