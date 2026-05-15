namespace Innovayse.Application.Services.Queries.ListCancellationRequests;

using Innovayse.Application.Common;
using Innovayse.Application.Services.DTOs;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Services;
using Innovayse.Domain.Services.Interfaces;

/// <summary>Handles listing cancellation requests with enriched service and client data.</summary>
public sealed class ListCancellationRequestsHandler(
    ICancellationRequestRepository cancellationRepo,
    IClientServiceRepository serviceRepo,
    IProductRepository productRepo,
    IClientRepository clientRepo)
{
    /// <summary>
    /// Handles <see cref="ListCancellationRequestsQuery"/>.
    /// Enriches each request with the product name and client full name.
    /// </summary>
    /// <param name="qry">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged result of cancellation request DTOs.</returns>
    public async Task<PagedResult<CancellationRequestDto>> HandleAsync(
        ListCancellationRequestsQuery qry, CancellationToken ct)
    {
        var (requests, total) = await cancellationRepo.ListAsync(qry.Page, qry.PageSize, ct);

        var dtos = new List<CancellationRequestDto>(requests.Count);

        foreach (var req in requests)
        {
            var service = await serviceRepo.FindByIdAsync(req.ServiceId, ct);
            var productName = "Unknown";
            var clientId = 0;
            var clientName = "Unknown";

            if (service is not null)
            {
                clientId = service.ClientId;

                var product = await productRepo.FindByIdAsync(service.ProductId, ct);
                productName = product?.Name ?? "Unknown";

                var client = await clientRepo.FindByIdAsync(service.ClientId, ct);
                clientName = client is not null
                    ? $"{client.FirstName} {client.LastName}"
                    : "Unknown";
            }

            dtos.Add(new CancellationRequestDto(
                req.Id,
                req.ServiceId,
                productName,
                clientId,
                clientName,
                FormatCancellationType(req.Type),
                req.Reason,
                req.Status.ToString(),
                req.CreatedAt));
        }

        return new PagedResult<CancellationRequestDto>(dtos, total, qry.Page, qry.PageSize);
    }

    /// <summary>Formats the cancellation type enum into a human-readable display string.</summary>
    /// <param name="type">The cancellation type.</param>
    /// <returns>"Immediate" or "End of Billing Period".</returns>
    private static string FormatCancellationType(CancellationType type) =>
        type switch
        {
            CancellationType.Immediate => "Immediate",
            CancellationType.EndOfBillingPeriod => "End of Billing Period",
            _ => type.ToString(),
        };
}
