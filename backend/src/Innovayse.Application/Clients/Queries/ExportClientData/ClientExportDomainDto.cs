namespace Innovayse.Application.Clients.Queries.ExportClientData;

using Innovayse.Domain.Domains;

/// <summary>One registered domain, as the <c>domains</c> section of a client data export lists it.</summary>
/// <param name="Id">The domain's primary key.</param>
/// <param name="Name">Fully qualified domain name.</param>
/// <param name="Status">Registration status.</param>
/// <param name="RegisteredAt">When the registration took effect.</param>
/// <param name="ExpiresAt">When the registration lapses unless it is renewed.</param>
public sealed record ClientExportDomainDto(
    int Id,
    string Name,
    DomainStatus Status,
    DateTimeOffset RegisteredAt,
    DateTimeOffset ExpiresAt);
