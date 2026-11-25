namespace Innovayse.Hostpanel.Application.Domains.Commands.SetAutoRenew;

/// <summary>
/// Command to enable or disable the panel's own automatic renewal for a domain. It never reaches the registrar:
/// <c>SetRegistrarAutoRenewCommand</c> owns the registrar's auto renew.
/// </summary>
/// <param name="DomainId">
/// Primary key of the domain to update. Nullable so the route id can overlay it: a non-nullable
/// member here would make the body required before <c>with { DomainId = id }</c> ever ran.
/// </param>
/// <param name="Value">
/// The desired panel renewal flag value. Nullable so a body that omits it reaches the validator and is refused with
/// 400, instead of binding to <see langword="false"/> and silently switching auto-renew off.
/// </param>
public sealed record SetAutoRenewCommand(int? DomainId, bool? Value);
