using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CreateOrderInvoice;

/// <summary>Handles <see cref="CreateOrderInvoiceCommand"/>: creates and saves the order's unpaid invoice.</summary>
public sealed class CreateOrderInvoiceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>How long an order's invoice stays open before it falls due.</summary>
    private readonly IOptions<OrderOptions> _orderOptions;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="orderOptions">How long an order's invoice stays open before it falls due.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    public CreateOrderInvoiceHandler(IHostpanelDbContext context, IOptions<OrderOptions> orderOptions, IClock clock)
    {
        _context = context;
        _orderOptions = orderOptions;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The order's client, currency and lines.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the new invoice id. This use case refuses nothing itself.</returns>
    public async Task<Result<int>> HandleAsync(CreateOrderInvoiceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var now = _clock.UtcNow;
        var invoice = Invoice.Create(cmd.ClientId, now.Add(_orderOptions.Value.InvoicePaymentTerm), cmd.Currency, now);
        foreach (var item in cmd.Items)
        {
            invoice.AddItem(item.Description, item.UnitPrice, item.Quantity, productSnapshot: item.ProductSnapshot);
        }

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(ct);

        return Result.Success(invoice.Id);
    }
}
