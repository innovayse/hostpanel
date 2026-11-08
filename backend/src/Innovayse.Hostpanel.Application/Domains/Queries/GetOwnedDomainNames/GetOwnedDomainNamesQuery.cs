namespace Innovayse.Hostpanel.Application.Domains.Queries.GetOwnedDomainNames;

/// <summary>
/// Asks which of the named domains one client holds, so an order can refuse to renew a domain
/// that is not the client's before any money is asked for, without reading the Domains table itself.
/// </summary>
/// <param name="ClientId">The client whose domains are checked.</param>
/// <param name="DomainNames">The names to check, in any letter case.</param>
/// <param name="LiveOnly">
/// Whether only a domain that is registered or on its way to be counts: active, pending registration or pending transfer.
/// An expired, redemption, cancelled or transferred-out domain is then not held, because it is not a registration an order
/// line can count as done. The default counts every domain the client has, which is what a renewal needs.
/// </param>
/// <param name="OrderId">
/// The order that must have created the domain, or <see langword="null"/> for any. A retry of an order line asks for its
/// own order's, so a domain the client holds from elsewhere is never counted as the line's registration.
/// </param>
public sealed record GetOwnedDomainNamesQuery(int ClientId, IReadOnlyList<string> DomainNames, bool LiveOnly = false, int? OrderId = null);
