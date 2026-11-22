using Innovayse.Hostpanel.Domain.Domains;

namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>
/// Adds one DNS record in a domain's zone at the registrar.
/// </summary>
/// <param name="Domain">The domain whose zone is changed.</param>
/// <param name="Record">The DNS record to add.</param>
public sealed record DnsRecordChange(DomainRef Domain, DnsRecord Record);
