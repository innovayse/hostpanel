namespace Innovayse.Hostpanel.Application.Auth.Common;

/// <summary>The account in another system that a client is linked to.</summary>
/// <param name="Provider">Short lower-case name of the system the account belongs to, e.g. <c>crm</c>.</param>
/// <param name="Id">The account's identifier in that system.</param>
public sealed record ExternalAccountDto(string Provider, string Id);
