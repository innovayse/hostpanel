using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Queries.ListUpcomingServiceRenewals;

/// <summary>
/// Handles <see cref="ListUpcomingServiceRenewalsQuery"/>: one page of the active services renewing inside
/// the window, with product and client names and the owner's billing currency.
/// </summary>
public sealed class ListUpcomingServiceRenewalsHandler
{
    /// <summary>The module's data context the services are read from.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The bus the product and client name queries are dispatched through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Resolves the currency each owning client is billed in.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>The clock the window starts from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context the services are read from.</param>
    /// <param name="bus">The bus the product and client name queries are dispatched through.</param>
    /// <param name="payerCurrency">Resolves the currency each owning client is billed in.</param>
    /// <param name="clock">The clock the window starts from.</param>
    public ListUpcomingServiceRenewalsHandler(
        IHostpanelDbContext context, IMessageBus bus, IPayerCurrencyResolver payerCurrency, IClock clock)
    {
        _context = context;
        _bus = bus;
        _payerCurrency = payerCurrency;
        _clock = clock;
    }

    /// <summary>Lists the page.</summary>
    /// <param name="query">The window and the page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, always: an out-of-range page answers an empty page.</returns>
    /// <exception cref="InvalidOperationException">A service names a product or a client that does not exist, so the database is broken.</exception>
    public async Task<Result<PaginatedList<UpcomingServiceRenewalDto>>> HandleAsync(
        ListUpcomingServiceRenewalsQuery query, CancellationToken ct)
    {
        var until = _clock.UtcNow.AddDays((int)query.Window);

        // Services already past due are included: they are the ones most in need of an invoice.
        var due = _context.ClientServices.AsNoTracking()
            .Where(s => s.Status == ServiceStatus.Active && s.NextRenewalAt != null && s.NextRenewalAt <= until);

        var total = await due.CountAsync(ct);
        var services = await due
            .OrderBy(s => s.NextRenewalAt).ThenBy(s => s.Id)
            .Paginate(query.Page, query.PageSize)
            .ToListAsync(ct);

        // FK_ClientServices_Products_ProductId and FK_ClientServices_Clients_ClientId keep both alive.
        var products = await _bus.GetRequiredProductSummariesByIdAsync(services.Select(s => s.ProductId).Distinct().ToList(), ct);
        var clientIds = services.Select(s => s.ClientId).Distinct().ToList();
        var clients = await _bus.GetRequiredClientBriefsByIdAsync(clientIds, ct);

        var currencies = new Dictionary<int, Currency>();
        foreach (var clientId in clientIds)
        {
            currencies[clientId] = await _payerCurrency.ForClientAsync(clientId, ct);
        }

        var items = services
            .Select(s => new UpcomingServiceRenewalDto(
                s.Id, s.ClientId, clients[s.ClientId].FullName, products[s.ProductId].Name, s.Domain,
                s.RecurringAmount, currencies[s.ClientId].Code, s.BillingCycle, s.NextRenewalAt!.Value, s.AutoRenew))
            .ToList();

        return Result<PaginatedList<UpcomingServiceRenewalDto>>.Success(
            new PaginatedList<UpcomingServiceRenewalDto>(items, total, query.Page, query.PageSize));
    }
}
