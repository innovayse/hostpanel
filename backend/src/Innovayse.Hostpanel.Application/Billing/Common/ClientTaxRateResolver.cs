using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// The one place that decides how a new invoice for a client is taxed. Every use case that creates a customer-facing
/// invoice from lines asks it (<see cref="ApplyToAsync"/>, or <see cref="ForInvoiceAsync(ClientBriefDto, Invoice, CancellationToken)"/>
/// and <see cref="Apply"/>), which fixes the answer on the invoice, so a later change of the rules or of the client never
/// alters an invoice that was issued.
/// </summary>
/// <remarks>
/// The rate is 0 when tax is switched off (<see cref="BillingSettingKeys.TaxEnabled"/>), when the client is tax exempt (the manual
/// override, which wins over everything below), or when the client has no country. A business client whose VAT number last
/// checked valid, no longer ago than <see cref="ScheduledJobsOptions.VatCheckValidForDays"/> (an older verdict nobody renewed no
/// longer counts, so the reverse charge fails closed), in an EU country other than the seller's (<see cref="BillingSettingKeys.SellerCountry"/>) or in the United
/// Kingdom, is invoiced under the reverse charge: rate 0, and the invoice states it and shows their number. A client in the
/// seller's own country is taxed, and so is one whose number is invalid, never checked or missing. For a consumer (no tax
/// identification number) in an EU member state other than the seller's, an invoice whose every line is a digital service is
/// taxed at the standard VAT rate of the client's country that was in force on the invoice date (<see cref="VatRate"/>). Every
/// other invoice takes the enabled tax rule of the client's region, or else the enabled country-wide rule, and none when no
/// rule matches.
/// </remarks>
public sealed class ClientTaxRateResolver
{
    /// <summary>The module's data context, for the tax rules and the VAT rates.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the global tax switch and the seller's country.</summary>
    private readonly SettingSource _settings;

    /// <summary>Wolverine bus the client's brief is read through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The clock a VAT number check's age is judged against.</summary>
    private readonly IClock _clock;

    /// <summary>The scheduled jobs settings, for how long a valid VAT number check keeps the reverse charge.</summary>
    private readonly IOptions<ScheduledJobsOptions> _options;

    /// <summary>Initializes the resolver.</summary>
    /// <param name="context">The module's data context, for the tax rules and the VAT rates.</param>
    /// <param name="settings">Reads the global tax switch and the seller's country.</param>
    /// <param name="bus">Wolverine bus the client's brief is read through.</param>
    /// <param name="clock">The clock a VAT number check's age is judged against.</param>
    /// <param name="options">The scheduled jobs settings, for how long a valid VAT number check keeps the reverse charge.</param>
    public ClientTaxRateResolver(
        IHostpanelDbContext context,
        SettingSource settings,
        IMessageBus bus,
        IClock clock,
        IOptions<ScheduledJobsOptions> options)
    {
        _context = context;
        _settings = settings;
        _bus = bus;
        _clock = clock;
        _options = options;
    }

    /// <summary>Fixes the tax of a freshly raised invoice, after its lines are added and before any credit is applied to it.</summary>
    /// <param name="invoice">The new, still editable invoice of the client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>CLIENT_NOT_FOUND</c> when the invoice names no client record.</returns>
    public async Task<Result> ApplyToAsync(Invoice invoice, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var treatment = await ForInvoiceAsync(invoice, ct);
        if (treatment.IsFailure)
        {
            return Result.FailureCode(treatment.ErrorCode!);
        }

        Apply(invoice, treatment.Value!);
        return Result.Success();
    }

    /// <summary>Fixes a treatment on an invoice: the reverse charge with the client's number, or the rate.</summary>
    /// <param name="invoice">The new, still editable invoice.</param>
    /// <param name="treatment">What <see cref="ForInvoiceAsync(ClientBriefDto, Invoice, CancellationToken)"/> answered for its client.</param>
    public static void Apply(Invoice invoice, ClientTaxTreatment treatment)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(treatment);

        if (treatment.ReverseChargeVatNumber is { } vatNumber)
        {
            invoice.ApplyReverseCharge(vatNumber);
            return;
        }

        invoice.ApplyTaxRate(treatment.Rate);
    }

    /// <summary>Refuses a tax rate typed by hand on the invoice of a client whose tax is not the operator's to enter.</summary>
    /// <param name="clientId">The client record's id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success when the client is liable to tax, <c>CLIENT_NOT_FOUND</c> when the id names no record,
    /// <c>CLIENT_TAX_EXEMPT</c> for a tax exempt client, or <c>CLIENT_REVERSE_CHARGE_APPLIES</c> for one invoiced under the reverse charge.
    /// </returns>
    public async Task<Result> EnsureMayBeTaxedAsync(int clientId, CancellationToken ct)
    {
        var client = await _bus.FindClientBriefAsync(clientId, withEmail: false, ct);
        if (client is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        }

        if (client.Settings.TaxExempt)
        {
            return Result.FailureCode(HostpanelErrorCodes.CLIENT_TAX_EXEMPT);
        }

        var (taxEnabled, sellerCountry) = await ReadTaxSwitchesAsync(ct);
        return taxEnabled && IsReverseCharge(client, sellerCountry)
            ? Result.FailureCode(HostpanelErrorCodes.CLIENT_REVERSE_CHARGE_APPLIES)
            : Result.Success();
    }

    /// <summary>How a new invoice is taxed, once its lines are added.</summary>
    /// <param name="invoice">The new invoice of the client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the treatment, or <c>CLIENT_NOT_FOUND</c> when the invoice names no client record.</returns>
    public async Task<Result<ClientTaxTreatment>> ForInvoiceAsync(Invoice invoice, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var client = await _bus.FindClientBriefAsync(invoice.ClientId, withEmail: false, ct);

        return client is null
            ? Result<ClientTaxTreatment>.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND)
            : Result.Success(await ForInvoiceAsync(client, invoice, ct));
    }

    /// <summary>How a new invoice is taxed, for a caller that has read the client's brief already.</summary>
    /// <param name="client">The brief of the invoice's client.</param>
    /// <param name="invoice">The new invoice of the client, with its lines.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The treatment; rate 0 and no reverse charge when no tax applies.</returns>
    public async Task<ClientTaxTreatment> ForInvoiceAsync(ClientBriefDto client, Invoice invoice, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(invoice);

        if (client.Settings.TaxExempt || string.IsNullOrWhiteSpace(client.Address.Country))
        {
            return new ClientTaxTreatment(0m);
        }

        var (taxEnabled, sellerCountry) = await ReadTaxSwitchesAsync(ct);
        if (!taxEnabled)
        {
            return new ClientTaxTreatment(0m);
        }

        if (IsReverseCharge(client, sellerCountry))
        {
            return new ClientTaxTreatment(0m, client.Settings.TaxId);
        }

        var country = client.Address.Country.Trim().ToUpperInvariant();
        if (IsDestinationRateDue(client, invoice, country, sellerCountry))
        {
            var vatRate = await StandardVatRateAsync(country, DateOnly.FromDateTime(invoice.InvoiceDate.UtcDateTime), ct);
            if (vatRate is { } destinationRate)
            {
                return new ClientTaxTreatment(destinationRate);
            }
        }

        return new ClientTaxTreatment(await RuleRateAsync(client, country, ct));
    }

    /// <summary>Reads the two settings every tax decision starts from.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether tax is switched on, and the seller's country (empty when the operator has set none).</returns>
    private async Task<(bool TaxEnabled, string SellerCountry)> ReadTaxSwitchesAsync(CancellationToken ct)
    {
        var rows = await _settings.ReadRowsAsync([BillingSettingKeys.TaxEnabled, BillingSettingKeys.SellerCountry], ct);

        return (bool.Parse(_settings.Value(rows, BillingSettingKeys.TaxEnabled)), _settings.Value(rows, BillingSettingKeys.SellerCountry));
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
    /// <param name="sellerCountry">The seller's country, or empty when none is set.</param>
    /// <returns><see langword="true"/> when the destination rate applies.</returns>
    private static bool IsDestinationRateDue(ClientBriefDto client, Invoice invoice, string country, string sellerCountry) =>
        IsOtherCountry(country, sellerCountry)
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

    /// <summary>
    /// Whether the reverse charge applies: the client holds a VAT number that last checked valid, no longer ago than
    /// <see cref="ScheduledJobsOptions.VatCheckValidForDays"/>, in a country with a VAT register (an EU member state or the
    /// United Kingdom) other than the seller's own. With no seller country set it never applies. A verdict nobody renewed
    /// within that time stops counting, so a number that went invalid while the re-check was not running cannot keep the reverse charge.
    /// </summary>
    /// <param name="client">The client's brief.</param>
    /// <param name="sellerCountry">The seller's country as stored; empty when the operator has set none.</param>
    /// <returns><see langword="true"/> when the invoice is raised under the reverse charge.</returns>
    private bool IsReverseCharge(ClientBriefDto client, string sellerCountry) =>
        client.Vat is { Status: VatCheckOutcome.Valid, CheckedAt: { } checkedAt }
            && checkedAt > _clock.UtcNow - _options.Value.VatCheckValidFor
            && !string.IsNullOrWhiteSpace(client.Settings.TaxId)
            && VatRegisters.Covers(client.Address.Country)
            && IsOtherCountry(client.Address.Country!, sellerCountry);

    /// <summary>Whether a client's country is a country other than the seller's. With no seller country set, none is.</summary>
    /// <param name="country">The client's country code.</param>
    /// <param name="sellerCountry">The seller's country as stored; empty when the operator has set none.</param>
    /// <returns><see langword="true"/> when a seller country is set and differs from the client's.</returns>
    private static bool IsOtherCountry(string country, string sellerCountry) =>
        sellerCountry.Length > 0 && !string.Equals(country.Trim(), sellerCountry, StringComparison.OrdinalIgnoreCase);
}
