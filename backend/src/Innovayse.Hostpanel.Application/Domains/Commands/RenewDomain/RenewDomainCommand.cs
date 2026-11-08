namespace Innovayse.Hostpanel.Application.Domains.Commands.RenewDomain;

/// <summary>Command to renew an existing domain registration for additional years.</summary>
/// <param name="DomainId">
/// Primary key of the domain to renew. Nullable so the route id can overlay it: a non-nullable
/// member here would make the body required before <c>with { DomainId = id }</c> ever ran.
/// </param>
/// <param name="Years">Number of years to extend the registration (1 to 10).</param>
/// <param name="OrderItemId">
/// The order line the renewal is bought on, set only by order fulfilment and stored with the renewal so a retry of the
/// line can tell its own renewal from none; <see langword="null"/> for every other renewal. The controller overlays it
/// with <see langword="null"/>, so a body can never set it.
/// </param>
public sealed record RenewDomainCommand(int? DomainId, int Years, int? OrderItemId = null);
