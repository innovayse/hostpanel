using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// Answers whether money in a given currency can go on an invoiced client's credit balance at its
/// face value. The balance carries no currency of its own; it is kept in the client's billing
/// currency, so money in any other currency would be credited at the wrong value.
/// </summary>
public static class ClientBillingCurrencyExtensions
{
    /// <summary>Whether <paramref name="currency"/> is the billing currency of the invoice's client.</summary>
    /// <param name="bus">The Wolverine bus the client is read through.</param>
    /// <param name="invoice">The invoice; its currency stands in for a client with no billing currency set.</param>
    /// <param name="currency">ISO 4217 code of the money to credit.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when crediting the money keeps its value.</returns>
    /// <exception cref="InvalidOperationException">The invoice names a client that does not exist, so the database is broken.</exception>
    public static async Task<bool> IsClientBillingCurrencyAsync(
        this IMessageBus bus, Invoice invoice, string currency, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(invoice);

        // FK_Invoices_Clients_ClientId restricts deleting a client that has invoices, so the client is there.
        var client = await bus.GetRequiredClientBriefAsync(invoice.ClientId, withEmail: false, ct);
        return string.Equals(CreditCurrency(client.Currency, invoice.Currency), currency, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The currency a client's stored credit balance is held in, for a read that has no invoice at hand.</summary>
    /// <remarks>
    /// Applies the same rule as <see cref="IsClientBillingCurrencyAsync"/>: the client's billing currency, and
    /// for a client with none the currency of an invoice. A client with no currency can only hold credit that
    /// came from one of their invoices, so the most recent invoice stands in. With no credit there is nothing to
    /// place, and with no invoice there is nothing the credit could have come from, so only then is the base
    /// currency used.
    /// </remarks>
    /// <param name="context">The module's data context.</param>
    /// <param name="client">The client, or <see langword="null"/> when the id names nobody.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>ISO 4217 code of the currency the credit balance is in.</returns>
    public static async Task<string> GetCreditCurrencyAsync(
        this IHostpanelDbContext context, Client? client, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (client?.Currency is { } own)
        {
            return own;
        }

        var invoiceCurrency = client is null || client.CreditBalance == 0m
            ? null
            : await context.Invoices.AsNoTracking()
                .Where(i => i.ClientId == client.Id)
                .OrderByDescending(i => i.Id)
                .Select(i => i.Currency)
                .FirstOrDefaultAsync(ct);

        return CreditCurrency(null, invoiceCurrency ?? (await context.GetBaseCurrencyAsync(ct)).Code);
    }

    /// <summary>The one rule: a client's own billing currency, else the currency of the invoice standing in.</summary>
    /// <param name="clientCurrency">The client's billing currency, or <see langword="null"/>.</param>
    /// <param name="standIn">The invoice currency used when the client has none.</param>
    /// <returns>ISO 4217 code of the credit balance's currency.</returns>
    private static string CreditCurrency(string? clientCurrency, string standIn) => clientCurrency ?? standIn;
}
