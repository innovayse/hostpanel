namespace Innovayse.Application.Domains.DTOs;

using Innovayse.Domain.Domains;

/// <summary>DTO representing a domain in a list — summary data for table/grid display.</summary>
/// <param name="Id">Domain primary key.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="Name">Domain name including TLD (e.g. "example.com").</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="ExpiresAt">Domain expiration date (UTC).</param>
/// <param name="AutoRenew">Whether the domain is set to auto-renew at expiration.</param>
public record DomainListItemDto(int Id, int ClientId, string Name, DomainStatus Status, DateTimeOffset ExpiresAt, bool AutoRenew);
