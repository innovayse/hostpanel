namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>The parts of a client row the plugin host API builds a profile from.</summary>
/// <param name="FirstName">The client's first name.</param>
/// <param name="LastName">The client's last name.</param>
/// <param name="CompanyName">The client's company name; the profile shows it instead of the person's name when set.</param>
/// <param name="OwnerSubject">
/// The identity subject of the account's owner, used to read the sign-in email and language; empty when the client has no
/// owner account.
/// </param>
public sealed record PluginHostClientDto(string FirstName, string LastName, string? CompanyName, string OwnerSubject);
