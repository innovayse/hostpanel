namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>
/// Deletes one email forwarding rule of a domain at the registrar.
/// </summary>
/// <param name="Domain">The domain at the registrar.</param>
/// <param name="Source">Source alias or local part to delete.</param>
public sealed record EmailForwardingRuleRemoval(DomainRef Domain, string Source);
