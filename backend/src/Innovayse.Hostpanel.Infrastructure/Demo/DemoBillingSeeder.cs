using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// Seeds the money side of the history: twelve months of invoices for every customer in the statuses the
/// entity can reach (draft, unpaid, overdue, paid, cancelled, refunded), the payments and refunds behind them,
/// the ledger rows those produce, and the credit top-ups that match the customers' credit balances. The status
/// of an invoice is a function of the customer, the customer's state and the month, so the revenue charts have
/// a shape and every reset reproduces it exactly.
/// </summary>
internal sealed class DemoBillingSeeder
{
    /// <summary>Days between an invoice date and its due date.</summary>
    private const int PaymentTermDays = 14;

    /// <summary>The card processing fee as a share of the payment, rounded to cents.</summary>
    private const decimal CardFeeRate = 0.029m;

    /// <summary>Value-added tax applied to invoices of customers billed in the second currency (a fictional EU style VAT).</summary>
    private const decimal LocalTaxRate = 20m;

    /// <summary>How often, in months, a domain renewal line is added to the monthly invoice.</summary>
    private const int DomainRenewalEvery = 3;

    /// <summary>The data context the rows are added to and saved through.</summary>
    private readonly AppDbContext _db;

    /// <summary>The anchor dates are measured from.</summary>
    private readonly DateTimeOffset _today;

    /// <summary>Creates the seeder.</summary>
    /// <param name="db">The data context the rows are added to and saved through.</param>
    /// <param name="today">The anchor dates are measured from.</param>
    public DemoBillingSeeder(AppDbContext db, DateTimeOffset today)
    {
        _db = db;
        _today = today;
    }

    /// <summary>Seeds the invoices, the ledger and the credit top-ups that are not there yet.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    public async Task SeedAsync(CancellationToken ct)
    {
        if (!await _db.Invoices.AnyAsync(ct))
        {
            await SeedInvoicesAsync(ct);
        }

        if (!await _db.Transactions.AnyAsync(ct))
        {
            await SeedLedgerAsync(ct);
        }
    }

    /// <summary>
    /// The status an invoice ends up in. Recent months are open or overdue, older ones are settled, with the odd
    /// refund and cancellation; a suspended customer is behind, a closed customer stopped being billed.
    /// </summary>
    /// <param name="client">The customer.</param>
    /// <param name="position">The customer's position in the id order, the demo client first.</param>
    /// <param name="monthsBack">How many months before the current one the invoice falls.</param>
    /// <returns>The status, or null when the customer is not billed that month.</returns>
    private static InvoiceStatus? StatusFor(Client client, int position, int monthsBack)
    {
        if (client.Status == ClientStatus.Closed)
        {
            return monthsBack >= 3 ? InvoiceStatus.Paid : null;
        }

        if (client.Status == ClientStatus.Suspended && monthsBack < 3)
        {
            return monthsBack == 0 ? InvoiceStatus.Unpaid : InvoiceStatus.Overdue;
        }

        // The demo client sees every status the billing area can show: open, overdue, paid, cancelled and refunded
        // (month eleven, from the general rule below), plus the draft added next to the current month.
        if (position == 0 && monthsBack == 4)
        {
            return InvoiceStatus.Cancelled;
        }

        if (position == 0 && monthsBack <= 2)
        {
            return monthsBack switch
            {
                0 => InvoiceStatus.Unpaid,
                1 => InvoiceStatus.Overdue,
                _ => InvoiceStatus.Paid,
            };
        }

        return monthsBack switch
        {
            0 => position % 4 == 0 ? InvoiceStatus.Draft : InvoiceStatus.Unpaid,
            1 => (position % 3) switch
            {
                0 => InvoiceStatus.Overdue,
                1 => InvoiceStatus.Unpaid,
                _ => InvoiceStatus.Paid,
            },
            2 => position % 5 == 0 ? InvoiceStatus.Overdue : InvoiceStatus.Paid,
            _ when (position + monthsBack) % 11 == 0 => InvoiceStatus.Refunded,
            _ when (position + monthsBack) % 13 == 0 => InvoiceStatus.Cancelled,
            _ => InvoiceStatus.Paid,
        };
    }

    /// <summary>Seeds every customer's invoices, newest last, with the payments and refunds on the settled ones.</summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedInvoicesAsync(CancellationToken ct)
    {
        var clients = await _db.Clients.OrderBy(c => c.Id).ToListAsync(ct);
        var products = await _db.Products.Include(p => p.Prices).ToDictionaryAsync(p => p.Id, ct);
        var services = (await _db.ClientServices.OrderBy(s => s.Id).ToListAsync(ct))
            .GroupBy(s => s.ClientId)
            .ToDictionary(g => g.Key, g => g.ToList());
        if (clients.Count == 0 || products.Count == 0)
        {
            return;
        }

        for (var c = 0; c < clients.Count; c++)
        {
            var client = clients[c];
            var clientServices = services.GetValueOrDefault(client.Id) ?? [];
            for (var monthsBack = DemoAnchor.FirstBillingMonthBack(c); monthsBack >= 0; monthsBack--)
            {
                var status = StatusFor(client, c, monthsBack);
                if (status is null)
                {
                    continue;
                }

                var service = clientServices.Count == 0 ? null : clientServices[(c + monthsBack) % clientServices.Count];
                _db.Invoices.Add(BuildInvoice(client, c, monthsBack, status.Value, service, service is null ? null : products[service.ProductId]));
            }

            // A draft the demo client's account manager is still preparing, beside the current unpaid invoice.
            if (c == 0)
            {
                var draftService = clientServices.Count == 0 ? null : clientServices[^1];
                _db.Invoices.Add(BuildInvoice(
                    client, c, 0, InvoiceStatus.Draft, draftService, draftService is null ? null : products[draftService.ProductId]));
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Builds one invoice with its lines, tax, backdating and the payment history its status implies.</summary>
    /// <param name="client">The customer.</param>
    /// <param name="position">The customer's position in the id order.</param>
    /// <param name="monthsBack">How many months before the current one the invoice falls.</param>
    /// <param name="status">The status to reach.</param>
    /// <param name="service">The service the main line bills, or null when the customer has none.</param>
    /// <param name="product">The service's product, or null when there is no service.</param>
    /// <returns>The invoice, not yet saved.</returns>
    private Invoice BuildInvoice(Client client, int position, int monthsBack, InvoiceStatus status, ClientService? service, Product? product)
    {
        var currency = client.Currency ?? DemoCatalogueSeeder.BaseCurrency;
        var invoiceDate = _today.AddMonths(-monthsBack).AddDays(-(position % 7));
        var dueDate = invoiceDate.AddDays(PaymentTermDays);

        var invoice = status == InvoiceStatus.Draft
            ? Invoice.CreateDraft(client.Id, dueDate, currency, invoiceDate)
            : Invoice.Create(client.Id, dueDate, currency, invoiceDate);

        var price = product?.PriceFor(currency, BillingCycle.Monthly) ?? service?.RecurringAmount ?? 0m;
        invoice.AddItem($"{product?.Name ?? "Hosting"} - monthly", price, 1, service?.Id);
        if (monthsBack % DomainRenewalEvery == 0)
        {
            var renewal = currency == DemoCatalogueSeeder.BaseCurrency ? 14.99m : DemoCatalogueSeeder.ToEuros(14.99m);
            invoice.AddItem($"Domain renewal - {DemoSites.HostOf(client)}", renewal, 1);
        }

        invoice.Backdate(invoiceDate);
        invoice.UpdateOptions(invoiceDate, dueDate, PaymentMethodNames.Card, currency == DemoCatalogueSeeder.LocalCurrency ? LocalTaxRate : 0m);

        switch (status)
        {
            case InvoiceStatus.Overdue:
                invoice.MarkOverdue(dueDate);
                break;
            case InvoiceStatus.Cancelled:
                invoice.Cancel();
                break;
            case InvoiceStatus.Paid:
            case InvoiceStatus.Refunded:
                RecordPayment(invoice, client, position, monthsBack, invoiceDate, status == InvoiceStatus.Refunded);
                break;
            default:
                break;
        }

        return invoice;
    }

    /// <summary>Records the payment on an invoice, by card for most and by bank transfer for every fourth, and a refund when asked.</summary>
    /// <param name="invoice">The invoice to settle.</param>
    /// <param name="client">The customer.</param>
    /// <param name="position">The customer's position in the id order.</param>
    /// <param name="monthsBack">How many months before the current one the invoice falls.</param>
    /// <param name="invoiceDate">When the invoice was raised.</param>
    /// <param name="refund">Whether the payment is then refunded in full.</param>
    private static void RecordPayment(Invoice invoice, Client client, int position, int monthsBack, DateTimeOffset invoiceDate, bool refund)
    {
        var byCard = (position + monthsBack) % 4 != 0;
        var gateway = byCard ? PaymentMethodNames.Card : PaymentMethodNames.BankTransfer;
        var fees = byCard ? Math.Round(invoice.Total * CardFeeRate, 2) : 0m;
        var paidAt = invoiceDate.AddDays(2 + ((position + monthsBack) % 9));

        var source = byCard ? TransactionSource.GatewayPayment : TransactionSource.Manual;
        invoice.AddPayment(paidAt, gateway, $"demo_pay_{client.Id}_{monthsBack}", invoice.Total, fees, null, source);
        if (refund)
        {
            invoice.AddRefund(paidAt.AddDays(5), gateway, $"demo_refund_{client.Id}_{monthsBack}", invoice.Total, 0m, "Refunded at the customer's request.",
                addedToCredit: false, source: byCard ? TransactionSource.GatewayRefund : TransactionSource.Manual);
        }
    }

    /// <summary>
    /// Seeds the ledger the payments produced (one row per payment, one per refund, as the real flows write them)
    /// and a top-up row for every customer who starts with a credit balance.
    /// </summary>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>A task that completes once the rows are saved.</returns>
    private async Task SeedLedgerAsync(CancellationToken ct)
    {
        var invoices = await _db.Invoices.Include(i => i.Transactions).OrderBy(i => i.Id).ToListAsync(ct);
        foreach (var invoice in invoices)
        {
            foreach (var payment in invoice.Transactions.OrderBy(t => t.Date))
            {
                if (payment.Type == InvoiceTransactionType.Payment)
                {
                    _db.Transactions.Add(Transaction.CreateInvoicePayment(
                        invoice, payment.Date, payment.Gateway, payment.TransactionId, payment.Amount, payment.Fees,
                        GatewayOrManual(payment.Gateway, TransactionSource.GatewayPayment)));
                }
                else if (payment.Type == InvoiceTransactionType.Refund)
                {
                    _db.Transactions.Add(Transaction.Create(
                        invoice.ClientId, payment.Date, $"Invoice #{invoice.Id} Refund", payment.TransactionId, invoice.Id,
                        payment.Gateway, 0m, payment.Amount, payment.Fees, false, invoice.Currency,
                        GatewayOrManual(payment.Gateway, TransactionSource.GatewayRefund)));
                }
            }
        }

        var credited = await _db.Clients.Where(c => c.CreditBalance > 0).OrderBy(c => c.Id).ToListAsync(ct);
        foreach (var client in credited)
        {
            _db.Transactions.Add(Transaction.Create(
                client.Id, _today.AddDays(-20), "Account credit top-up", $"demo_credit_{client.Id}", null,
                PaymentMethodNames.Card, client.CreditBalance, 0m, 0m, true,
                client.Currency ?? DemoCatalogueSeeder.BaseCurrency, TransactionSource.GatewayPayment));
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The ledger source of a demo payment or refund: the gateway source for a card, because a card is
    /// the one method the demo takes through a gateway, and a manual entry for any other method.
    /// </summary>
    /// <param name="paymentMethod">The method the payment or refund was recorded with.</param>
    /// <param name="gatewaySource">The gateway source of the row's direction.</param>
    /// <returns><paramref name="gatewaySource"/> for a card, otherwise <see cref="TransactionSource.Manual"/>.</returns>
    private static TransactionSource GatewayOrManual(string paymentMethod, TransactionSource gatewaySource)
    {
        return paymentMethod == PaymentMethodNames.Card ? gatewaySource : TransactionSource.Manual;
    }
}
