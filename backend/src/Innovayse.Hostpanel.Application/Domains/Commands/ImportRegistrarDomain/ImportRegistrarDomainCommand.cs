namespace Innovayse.Hostpanel.Application.Domains.Commands.ImportRegistrarDomain;

/// <summary>
/// Command to bring a domain a registrar account already holds into the panel for a client, with its real expiry and status,
/// without an order, an invoice or a charge. The registrar's reference and expiry are read from the registrar's own list,
/// never taken from the caller.
/// </summary>
/// <param name="RegistrarModule">
/// The id of the installed registrar plugin that holds the domain, in any letter case. Nullable so the route value can
/// overlay it: a non-nullable member would make a body without it a binding error before <c>with { RegistrarModule = module }</c>
/// ever ran.
/// </param>
/// <param name="ClientId">The client that will own the domain.</param>
/// <param name="DomainName">The fully qualified name of the domain, as the registrar's list shows it.</param>
public sealed record ImportRegistrarDomainCommand(string? RegistrarModule, int ClientId, string DomainName);
