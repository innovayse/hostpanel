using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
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
/// The rate is 0 when tax is switched off (<see cref="BillingSettingKeys.TaxEnabled"/>), when the client is tax exempt, when the
/// client has no country, or when no enabled rule matches. Otherwise it is the enabled rule of the client's region, or
/// else the enabled country-wide rule.
/// </remarks>
public sealed class ClientTaxRateResolver
{
    /// <summary>The module's data context, for the tax rules.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the global tax switch.</summary>
    private readonly SettingSource _settings;

    /// <summary>Wolverine bus the client's brief is read through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the resolver.</summary>
    /// <param name="context">The module's data context, for the tax rules.</param>
    /// <param name="settings">Reads the global tax switch.</param>
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

        var rate = await ForClientAsync(invoice.ClientId, ct);
        if (rate.IsFailure)
        {
            return Result.FailureCode(rate.ErrorCode!);
        }

        invoice.ApplyTaxRate(rate.Value);
        return Result.Success();
    }

    /// <summary>The rate a new invoice for the client is raised at.</summary>
    /// <param name="clientId">The client record's id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the rate in percent (0 when none applies), or <c>CLIENT_NOT_FOUND</c> when the id names no record.</returns>
    public async Task<Result<decimal>> ForClientAsync(int clientId, CancellationToken ct)
    {
        var client = await _bus.FindClientBriefAsync(clientId, withEmail: false, ct);

        return client is null
            ? Result<decimal>.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND)
            : Result.Success(await ForClientAsync(client, ct));
    }

    /// <summary>The rate a new invoice for the client is raised at, for a caller that has read the client's brief already.</summary>
    /// <param name="client">The client's brief.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The rate in percent; 0 when none applies.</returns>
    public async Task<decimal> ForClientAsync(ClientBriefDto client, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);

        if (client.Settings.TaxExempt || string.IsNullOrWhiteSpace(client.Address.Country))
        {
            return 0m;
        }

        var rows = await _settings.ReadRowsAsync([BillingSettingKeys.TaxEnabled], ct);
        if (!bool.Parse(_settings.Value(rows, BillingSettingKeys.TaxEnabled)))
        {
            return 0m;
        }

        var country = client.Address.Country.Trim().ToUpperInvariant();
        var enabled = _context.TaxRules.AsNoTracking().Where(r => r.IsEnabled);

        // The region rule wins over the country-wide one; a client with no state is taxed at the country-wide rule only.
        var rule = string.IsNullOrWhiteSpace(client.Address.State)
            ? null
            : await enabled.ForPlace(country, client.Address.State).FirstOrDefaultAsync(ct);
        rule ??= await enabled.ForPlace(country, region: null).FirstOrDefaultAsync(ct);

        return rule?.RatePercent ?? 0m;
    }
}
