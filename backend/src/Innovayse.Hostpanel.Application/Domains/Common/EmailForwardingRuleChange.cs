namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>
/// Adds or updates one email forwarding rule of a domain at the registrar.
/// </summary>
/// <param name="Domain">The domain at the registrar.</param>
/// <param name="Source">Source alias or local part (e.g. "info").</param>
/// <param name="Destination">Destination email address.</param>
public sealed record EmailForwardingRuleChange(DomainRef Domain, string Source, string Destination);
