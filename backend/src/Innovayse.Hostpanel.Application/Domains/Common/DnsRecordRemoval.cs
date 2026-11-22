using Innovayse.Hostpanel.Domain.Domains;

namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>
/// Deletes one DNS record from a domain's zone at the registrar.
/// </summary>
/// <param name="Domain">The domain whose zone is changed.</param>
/// <param name="Record">The record to delete, as it stood before it was removed from the domain; the registrar finds it by what it holds.</param>
public sealed record DnsRecordRemoval(DomainRef Domain, DnsRecord Record);
