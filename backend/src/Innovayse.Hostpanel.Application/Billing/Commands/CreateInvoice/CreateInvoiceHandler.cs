using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CreateInvoice;

/// <summary>Creates a new invoice with the provided line items and persists it.</summary>
public sealed class CreateInvoiceHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Records the activity log line for this action, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Resolves the currency the invoiced client is billed in, stamped on the invoice.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>The clock the issue moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Wolverine bus the owning modules' existence queries are dispatched through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Fixes the tax rate the invoice is raised at.</summary>
    private readonly ClientTaxRateResolver _taxRates;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="activity">Records the activity log line for this action, saved with the change it describes.</param>
    /// <param name="payerCurrency">The currency the invoiced client is billed in, stamped on the invoice.</param>
    /// <param name="clock">The clock the issue moment is read from.</param>
    /// <param name="bus">Wolverine bus the owning modules' existence queries are dispatched through.</param>
    /// <param name="taxRates">Fixes the tax rate the invoice is raised at.</param>
    public CreateInvoiceHandler(
        IHostpanelDbContext context, IActivityLogger activity, IPayerCurrencyResolver payerCurrency, IClock clock,
        IMessageBus bus, ClientTaxRateResolver taxRates)
    {
        _context = context;
        _activity = activity;
        _payerCurrency = payerCurrency;
        _clock = clock;
        _bus = bus;
        _taxRates = taxRates;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The create invoice command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the newly created invoice ID, or <c>CLIENT_NOT_FOUND</c> when the id names no client.</returns>
    public async Task<Result<int>> HandleAsync(CreateInvoiceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (!await _bus.ClientExistsAsync(cmd.ClientId, ct))
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        }

        var currency = (await _payerCurrency.ForClientAsync(cmd.ClientId, ct)).Code;
        var invoice = Invoice.Create(cmd.ClientId, cmd.DueDate, currency, cmd.IsDraft, _clock.UtcNow);

        foreach (var item in cmd.Items)
        {
            invoice.AddItem(item.Description, item.UnitPrice, item.Quantity);
        }

        var taxed = await _taxRates.ApplyToAsync(invoice, ct);
        if (taxed.IsFailure)
        {
            return Result<int>.FailureCode(taxed.ErrorCode!);
        }

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(ct);

        await _activity.RecordAsync(
            new ActivityLogEntry(cmd.ClientId, $"Created Manual Invoice - Invoice ID: {invoice.Id}"), ct);
        await _context.SaveChangesAsync(ct);

        return Result<int>.Success(invoice.Id);
    }
}
