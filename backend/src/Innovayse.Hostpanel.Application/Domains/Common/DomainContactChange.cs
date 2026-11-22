using Innovayse.Hostpanel.Domain.Domains;

namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>
/// Replaces the WHOIS registrant contact details of a domain at the registrar.
/// </summary>
/// <param name="Domain">The domain at the registrar.</param>
/// <param name="Contact">The updated contact details.</param>
public sealed record DomainContactChange(DomainRef Domain, DomainContact Contact);
