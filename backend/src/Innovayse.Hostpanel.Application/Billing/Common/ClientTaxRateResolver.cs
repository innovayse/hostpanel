using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// The one place that decides which tax rate a new invoice for a client is raised at. Every use case that creates a
/// customer-facing invoice from lines asks it (<see cref="ApplyToAsync"/>), which fixes the answer on the invoice with
/// <see cref="Invoice.ApplyTaxRate"/>, so a later change of the rules or of the client never alters an invoice that was issued.
/// </summary>
/// <remarks>
/// The rate is 0 when tax is switched off (<see cref="BillingSettingKeys.TaxEnabled"/>), when the client is tax exempt, or when the
/// client has no country. For a consumer in an EU member state other than the seller's
/// (<see cref="BillingSettingKeys.SellerCountry"/>), an invoice whose every line is a digital service is taxed at the standard VAT
/// rate of the client's country that was in force on the invoice date (<see cref="VatRate"/>). Every other invoice takes the
/// enabled tax rule of the client's region, or else the enabled country-wide rule, and none when no rule matches. A client who
/// has a tax identification number is a business, not a consumer: the reverse charge decision for business clients does not
/// belong here, so such a client keeps the rate of the tax rules.
/// </remarks>
public sealed class ClientTaxRateResolver
{
    /// <summary>The module's data context, for the tax rules and the VAT rates.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the global tax switch and the seller's country.</summary>
    private readonly SettingSource _settings;

    /// <summary>Wolverine bus the client's brief is read through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the resolver.</summary>
    /// <param name="context">The module's data context, for the tax rules and the VAT rates.</param>
    /// <param name="settings">Reads the global tax switch and the seller's country.</param>
    /// <param name="bus">Wolverine bus the client's brief is read through.</param>
    public ClientTaxRateResolver(IHostpanelDbContext context, SettingSource settings, IMessageBus bus)
    {
        _context = context;
        _settings = settings;
        _bus = bus;
    }

    /// <summary>Fixes the tax rate of a freshly raised invoice, after its lines are added and before any credit is applied to it.</summary>
    /// <param name="invoice">The new, still editable invoice of the client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>CLIENT_NOT_FOUND</c> when the invoice names no client record.</returns>
    public async Task<Result> ApplyToAsync(Invoice invoice, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var rate = await ForInvoiceAsync(invoice, ct);
        if (rate.IsFailure)
        {
            return Result.FailureCode(rate.ErrorCode!);
        }

        invoice.ApplyTaxRate(rate.Value);
        return Result.Success();
    }

    /// <summary>The rate a new invoice is raised at, once its lines are added.</summary>
    /// <param name="invoice">The new invoice of the client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the rate in percent (0 when none applies), or <c>CLIENT_NOT_FOUND</c> when the invoice names no client record.</returns>
    public async Task<Result<decimal>> ForInvoiceAsync(Invoice invoice, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var client = await _bus.FindClientBriefAsync(invoice.ClientId, withEmail: false, ct);

        return client is null
            ? Result<decimal>.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND)
            : Result.Success(await ForInvoiceAsync(client, invoice, ct));
    }

    /// <summary>The rate a new invoice is raised at, for a caller that has read the client's brief already.</summary>
    /// <param name="client">The brief of the invoice's client.</param>
    /// <param name="invoice">The new invoice of the client, with its lines.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The rate in percent; 0 when none applies.</returns>
    public async Task<decimal> ForInvoiceAsync(ClientBriefDto client, Invoice invoice, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(invoice);

        if (client.Settings.TaxExempt || string.IsNullOrWhiteSpace(client.Address.Country))
        {
            return 0m;
        }

        var rows = await _settings.ReadRowsAsync([BillingSettingKeys.TaxEnabled, BillingSettingKeys.SellerCountry], ct);
        if (!bool.Parse(_settings.Value(rows, BillingSettingKeys.TaxEnabled)))
        {
            return 0m;
        }

        var country = client.Address.Country.Trim().ToUpperInvariant();
        var sellerCountry = _settings.Value(rows, BillingSettingKeys.SellerCountry);
        if (IsDestinationRateDue(client, invoice, country, sellerCountry))
        {
            var vatRate = await StandardVatRateAsync(country, DateOnly.FromDateTime(invoice.InvoiceDate.UtcDateTime), ct);
            if (vatRate is { } destinationRate)
            {
                return destinationRate;
            }
        }

        return await RuleRateAsync(client, country, ct);
    }

    /// <summary>The standard VAT rate of a member state that was in force on a day.</summary>
    /// <param name="country">The member state's ISO 3166-1 alpha-2 code, upper case.</param>
    /// <param name="day">The day the rate is wanted for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The rate in percent, or <see langword="null"/> when the country has no standard rate that started on or before the day.</returns>
    private async Task<decimal?> StandardVatRateAsync(string country, DateOnly day, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(country);

        return await _context.VatRates.AsNoTracking()
            .InForceOn(country, VatRateKind.Standard, day)
            .Select(r => (decimal?)r.RatePercent)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Whether the invoice is taxed at the VAT rate of the client's country: a consumer (no tax identification number) in a
    /// country other than the seller's, billed only for digital services. An invoice with no lines is not.
    /// </summary>
    /// <param name="client">The brief of the invoice's client.</param>
    /// <param name="invoice">The invoice, with its lines.</param>
    /// <param name="country">The client's country, upper case.</param>
    /// <param name="sellerCountry">The seller's country, upper case, or empty when none is set.</param>
    /// <returns><see langword="true"/> when the destination rate applies.</returns>
    private static bool IsDestinationRateDue(ClientBriefDto client, Invoice invoice, string country, string sellerCountry) =>
        sellerCountry.Length > 0
        && !string.Equals(country, sellerCountry, StringComparison.Ordinal)
        && string.IsNullOrWhiteSpace(client.Settings.TaxId)
        && invoice.Items.Count > 0
        && invoice.Items.All(i => i.IsDigitalService);

    /// <summary>The rate of the enabled tax rule that matches the client's address.</summary>
    /// <param name="client">The brief of the invoice's client.</param>
    /// <param name="country">The client's country, upper case.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The rate in percent; 0 when no enabled rule matches.</returns>
    private async Task<decimal> RuleRateAsync(ClientBriefDto client, string country, CancellationToken ct)
    {
        var enabled = _context.TaxRules.AsNoTracking().Where(r => r.IsEnabled);

        // The region rule wins over the country-wide one; a client with no state is taxed at the country-wide rule only.
        var rule = string.IsNullOrWhiteSpace(client.Address.State)
            ? null
            : await enabled.ForPlace(country, client.Address.State).FirstOrDefaultAsync(ct);
        rule ??= await enabled.ForPlace(country, region: null).FirstOrDefaultAsync(ct);

        return rule?.RatePercent ?? 0m;
    }
}
