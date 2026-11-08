namespace Innovayse.Hostpanel.Application.Domains.Commands.RenewOrderedDomain;

/// <summary>Renews a domain named by a paid order line, for the client who placed the order.</summary>
/// <param name="ClientId">The client who paid for the renewal; the domain must be theirs.</param>
/// <param name="DomainName">Fully-qualified name from the order line.</param>
/// <param name="Years">Period bought, in years.</param>
/// <param name="OrderItemId">
/// The order line the renewal is bought on, or <see langword="null"/> when it is not a line's. The renewal is stored with
/// it, and a domain this same line already renewed is counted as renewed instead of being extended a second time.
/// </param>
public sealed record RenewOrderedDomainCommand(int ClientId, string DomainName, int Years, int? OrderItemId = null);
