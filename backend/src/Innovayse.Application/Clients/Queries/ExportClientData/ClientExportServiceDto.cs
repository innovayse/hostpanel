namespace Innovayse.Application.Clients.Queries.ExportClientData;

using Innovayse.Domain.Services;

/// <summary>
/// One purchased product or service, as the <c>productsServices</c> section of a client data
/// export lists it.
/// </summary>
/// <param name="Id">The service's primary key.</param>
/// <param name="ProductId">The product this service was ordered from.</param>
/// <param name="BillingCycle">How often it renews, e.g. Monthly / Annually.</param>
/// <param name="Status">Service status — Active, Suspended, Terminated and so on.</param>
/// <param name="CreatedAt">When the service was ordered.</param>
/// <param name="NextRenewalAt">When it next renews, or null when nothing further is due.</param>
public sealed record ClientExportServiceDto(
    int Id,
    int ProductId,
    string BillingCycle,
    ServiceStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? NextRenewalAt);
