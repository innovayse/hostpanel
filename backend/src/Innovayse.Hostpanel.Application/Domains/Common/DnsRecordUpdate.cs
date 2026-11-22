using Innovayse.Hostpanel.Domain.Domains;

namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>An update of one DNS record: the registrar finds the record by what it holds now and replaces it.</summary>
/// <param name="Domain">The domain at the registrar.</param>
/// <param name="Current">The record as it stood before the change.</param>
/// <param name="Updated">The record as it must be after the change.</param>
public sealed record DnsRecordUpdate(DomainRef Domain, DnsRecord Current, DnsRecord Updated);
