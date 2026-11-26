using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Billing.Currencies.Common;
using Innovayse.Hostpanel.Application.Billing.Currencies.Queries.ConvertAmounts;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Queries.GetClient;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Queries.GetTldRenewPrice;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Domains.Commands.ImportRegistrarDomain;

/// <summary>
/// Handles <see cref="ImportRegistrarDomainCommand"/>: finds the domain in the registrar's own list, so the reference and the
/// expiry are the registrar's and not the caller's, and stores it for the client through
/// <see cref="RegisteredDomain.ImportFromRegistrar"/>. No order, invoice or charge is made and no registrar operation that
/// changes anything is called. The recurring amount is the renew price of the extension for one year, in the client's billing
/// currency. The import is recorded in the client's activity log, saved with the domain.
/// </summary>
public sealed class ImportRegistrarDomainHandler
{
    /// <summary>
    /// The most pages read while looking for the domain in the registrar's list: a registrar that never ends its cursor is
    /// answered as not holding the domain instead of being asked for ever.
    /// </summary>
    private const int MaxScannedPages = 500;

    /// <summary>The renewal period, in years, whose price the recurring amount follows.</summary>
    private const int RenewalYears = 1;

    /// <summary>The data context the client and the existing domains are read from and the new domain is saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Builds the provider the chosen registrar module names.</summary>
    private readonly IRegistrarProviderFactory _factory;

    /// <summary>Tells which registrar plugins are installed, and how each spells its id.</summary>
    private readonly IRegistrarModuleCatalogue _modules;

    /// <summary>Decides the currency the client is billed in.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>Reads the extension's renew price and converts it into the client's currency, through the modules that own them.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Records the activity log line, saved with the domain.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context the client and the existing domains are read from and the new domain is saved through.</param>
    /// <param name="factory">Builds the provider the chosen registrar module names.</param>
    /// <param name="modules">Tells which registrar plugins are installed, and how each spells its id.</param>
    /// <param name="payerCurrency">Decides the currency the client is billed in.</param>
    /// <param name="bus">Reads the extension's renew price and converts it into the client's currency, through the modules that own them.</param>
    /// <param name="clock">The source of the current moment.</param>
    /// <param name="activity">Records the activity log line, saved with the domain.</param>
    public ImportRegistrarDomainHandler(
        IHostpanelDbContext context,
        IRegistrarProviderFactory factory,
        IRegistrarModuleCatalogue modules,
        IPayerCurrencyResolver payerCurrency,
        IMessageBus bus,
        IClock clock,
        IActivityLogger activity)
    {
        _context = context;
        _factory = factory;
        _modules = modules;
        _payerCurrency = payerCurrency;
        _bus = bus;
        _clock = clock;
        _activity = activity;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The registrar, the client and the domain name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The new domain's id, <c>CLIENT_NOT_FOUND</c> when the id names no client, <c>DOMAIN_ALREADY_REGISTERED</c> when the panel
    /// already has the name, <c>REGISTRAR_NOT_CONFIGURED</c> when the module names no installed registrar plugin,
    /// <c>REGISTRAR_DOMAIN_NOT_FOUND</c> when the registrar's list does not hold the name,
    /// <c>DOMAIN_IMPORT_EXPIRY_UNKNOWN</c> when the registrar does not report its expiry, or the <c>REGISTRAR_*</c> code the
    /// registrar refused to list with.
    /// </returns>
    public async Task<Result<int>> HandleAsync(ImportRegistrarDomainCommand cmd, CancellationToken ct)
    {
        if (cmd.RegistrarModule is null || _modules.FindId(cmd.RegistrarModule) is not { } module)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.REGISTRAR_NOT_CONFIGURED);
        }

        // The client belongs to the Clients module, so it is read through that module's query, never its table.
        var client = await _bus.InvokeAsync<Result<ClientDto>>(new GetClientQuery(cmd.ClientId), ct);
        if (!client.IsSuccess)
        {
            return Result<int>.FailureCode(client.ErrorCode ?? HostpanelErrorCodes.CLIENT_NOT_FOUND);
        }

        var storedName = RegisteredDomain.NormaliseName(cmd.DomainName);
        if (await _context.Domains.AsNoTracking().AnyAsync(d => d.Name == storedName, ct))
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.DOMAIN_ALREADY_REGISTERED);
        }

        var provider = await _factory.GetProviderAsync(module, ct);
        var held = await FindHeldAsync(provider, storedName, ct);
        if (held is not { IsSuccess: true, Value: { } listing })
        {
            return Result<int>.FailureCode(held.ErrorCode ?? HostpanelErrorCodes.HOSTPANEL_UNEXPECTED);
        }

        if (listing.ExpiresAt is not { } expiresAt)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.DOMAIN_IMPORT_EXPIRY_UNKNOWN);
        }

        var payer = await _payerCurrency.ForClientAsync(cmd.ClientId, ct);
        var recurring = await RenewPriceAsync(storedName, payer.Code, ct);
        if (recurring is not { IsSuccess: true, Value: var recurringAmount })
        {
            return Result<int>.FailureCode(recurring.ErrorCode ?? HostpanelErrorCodes.HOSTPANEL_UNEXPECTED);
        }

        var domain = RegisteredDomain.ImportFromRegistrar(
            cmd.ClientId, storedName, module, listing.RegistrarRef, expiresAt, _clock.UtcNow, payer.Code, recurringAmount);
        _context.Domains.Add(domain);
        await _activity.RecordAsync(
            new ActivityLogEntry(cmd.ClientId, $"Imported Domain From Registrar - Domain: {domain.Name}, Registrar: {module}"), ct);
        await _context.SaveChangesAsync(ct);
        return Result.Success(domain.Id);
    }

    /// <summary>Reads the registrar's list page by page until it names the domain.</summary>
    /// <param name="provider">The registrar that holds the account.</param>
    /// <param name="storedName">The domain's name in the form the panel stores it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The listed domain, <c>REGISTRAR_DOMAIN_NOT_FOUND</c> when the list ends without it, or the registrar's refusal code.</returns>
    private static async Task<Result<RegistrarHeldDomain>> FindHeldAsync(IRegistrarProvider provider, string storedName, CancellationToken ct)
    {
        string? cursor = null;
        for (var read = 0; read < MaxScannedPages; read++)
        {
            var page = await provider.ListDomainsAsync(cursor, ct);
            if (page is not { IsSuccess: true, Value: { } listed })
            {
                return Result<RegistrarHeldDomain>.FailureCode(page.ErrorCode ?? HostpanelErrorCodes.HOSTPANEL_UNEXPECTED);
            }

            var match = listed.Domains.FirstOrDefault(d => RegisteredDomain.NormaliseName(d.Name) == storedName);
            if (match is not null)
            {
                return Result.Success(match);
            }

            cursor = listed.NextCursor;
            if (cursor is null)
            {
                break;
            }
        }

        return Result<RegistrarHeldDomain>.FailureCode(HostpanelErrorCodes.REGISTRAR_DOMAIN_NOT_FOUND);
    }

    /// <summary>The renew price the extension of a name sets for one year, in the given currency.</summary>
    /// <param name="storedName">The domain's name; the longest configured extension of it is priced.</param>
    /// <param name="currency">The ISO 4217 code of the currency to price in.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The price; zero when the extension is not configured (<c>TLD_CONFIG_NOT_FOUND</c>), has no renew price for the year or
    /// has no exchange rate to convert it with, because an unpriced domain is still a domain the client holds; any other
    /// refusal, of the price read or of the conversion, is returned as the failure code.
    /// </returns>
    private async Task<Result<decimal>> RenewPriceAsync(string storedName, string currency, CancellationToken ct)
    {
        var configured = await _bus.InvokeAsync<Result<TldRenewPriceDto>>(new GetTldRenewPriceQuery(storedName, RenewalYears), ct);
        if (configured.ErrorCode == HostpanelErrorCodes.TLD_CONFIG_NOT_FOUND)
        {
            return Result.Success(0m);
        }

        if (configured is not { IsSuccess: true, Value: { } tld })
        {
            return Result<decimal>.FailureCode(configured.ErrorCode ?? HostpanelErrorCodes.HOSTPANEL_UNEXPECTED);
        }

        if (tld.SellPrice is not { } sell)
        {
            return Result.Success(0m);
        }

        var converted = await _bus.InvokeAsync<Result<ConvertedAmountsDto>>(
            new ConvertAmountsQuery(currency, [new CurrencyAmountDto(sell, tld.SellCurrency)]), ct);
        if (converted is not { IsSuccess: true, Value: { } convertedAmounts })
        {
            return Result<decimal>.FailureCode(converted.ErrorCode ?? HostpanelErrorCodes.HOSTPANEL_UNEXPECTED);
        }

        var price = convertedAmounts.Amounts[0];
        return Result.Success(price.Converted ? price.Amount : 0m);
    }
}
