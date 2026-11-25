namespace Innovayse.Hostpanel.Application.Domains.Commands.SetRegistrarAutoRenew;

/// <summary>
/// Command to turn automatic renewal on or off at the registrar that holds a domain. It never changes the panel's own
/// renewal flag, which <c>SetAutoRenewCommand</c> owns: the two are separate settings, so a domain the panel renews can
/// have auto renew off at the registrar and is not renewed a second time there.
/// </summary>
/// <param name="DomainId">
/// Primary key of the domain to update. Nullable so the route id can overlay it: a non-nullable
/// member here would make the body required before <c>with { DomainId = id }</c> ever ran.
/// </param>
/// <param name="Value">
/// The wanted registrar auto renew state. Nullable so a body that omits it reaches the validator and is refused with
/// 400, instead of binding to <see langword="false"/> and silently switching auto renew off at the registrar.
/// </param>
public sealed record SetRegistrarAutoRenewCommand(int? DomainId, bool? Value);
