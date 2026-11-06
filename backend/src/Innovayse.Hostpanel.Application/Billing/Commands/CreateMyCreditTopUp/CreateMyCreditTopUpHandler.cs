using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CreateMyCreditTopUp;

/// <summary>
/// Raises the invoice a client pays to add funds to their own credit balance. The invoice is one fixed line in the client's
/// billing currency and is paid through the ordinary invoice payment paths (a bank transfer or an installed gateway);
/// paying it puts the amount on the balance, once, in the save that marks it paid.
/// </summary>
public sealed class CreateMyCreditTopUpHandler
{
    /// <summary>How long a top-up invoice stays payable before its due date, in days.</summary>
    private const int DueInDays = 30;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the account the caller acts for, owner or linked user; the command does not say, and must not.</summary>
    private readonly IClientAccess _access;

    /// <summary>Resolves the currency the client is billed in, stamped on the invoice.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>Reads the top-up limits the admin set.</summary>
    private readonly CreditTopUpSettingsReader _settings;

    /// <summary>The clock the issue moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Records the activity log line for this action, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Wolverine bus, to ask the Clients module whether the account may transact.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="access">Resolves the account the caller acts for.</param>
    /// <param name="payerCurrency">Resolves the currency the client is billed in.</param>
    /// <param name="settings">Reads the top-up limits the admin set.</param>
    /// <param name="clock">The clock the issue moment is read from.</param>
    /// <param name="activity">Records the activity log line for this action, saved with the change it describes.</param>
    /// <param name="bus">Wolverine bus, to ask the Clients module whether the account may transact.</param>
    public CreateMyCreditTopUpHandler(
        IHostpanelDbContext context,
        IClientAccess access,
        IPayerCurrencyResolver payerCurrency,
        CreditTopUpSettingsReader settings,
        IClock clock,
        IActivityLogger activity,
        IMessageBus bus)
    {
        _context = context;
        _access = access;
        _payerCurrency = payerCurrency;
        _settings = settings;
        _clock = clock;
        _activity = activity;
        _bus = bus;
    }

    /// <summary>Handles <see cref="CreateMyCreditTopUpCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this raises the invoice for the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with the new invoice's id; <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller acts for no account;
    /// <c>CLIENT_SUSPENDED</c> or <c>CLIENT_CLOSED</c> when the account may not transact;
    /// <c>CREDIT_TOP_UP_AMOUNT_OUT_OF_RANGE</c> when the amount is below the minimum or above the maximum.
    /// </returns>
    public async Task<Result<int>> HandleAsync(CreateMyCreditTopUpCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var clientId = await _access.RequireClientIdAsync(ct);
        if (clientId.IsFailure)
        {
            return Result<int>.FailureCode(clientId.ErrorCode!);
        }

        var mayPay = await _bus.EnsureClientMayTransactAsync(clientId.Value, ct);
        if (mayPay.IsFailure)
        {
            return Result<int>.FailureCode(mayPay.ErrorCode!);
        }

        // The limits are in the client's billing currency, so the amount is rounded to that currency before it is compared:
        // the invoice carries the rounded figure, and a limit must hold for what the client is really asked to pay.
        var currency = (await _payerCurrency.ForClientAsync(clientId.Value, ct)).Code;
        var amount = CurrencyCode.RoundToMinorUnits(cmd.Amount, currency);
        var limits = await _settings.ReadAsync(ct);
        if (!limits.Allows(amount))
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.CREDIT_TOP_UP_AMOUNT_OUT_OF_RANGE);
        }

        var now = _clock.UtcNow;
        var invoice = Invoice.CreateCreditTopUp(clientId.Value, amount, currency, now, now.AddDays(DueInDays));
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(ct);

        await _activity.RecordAsync(
            new ActivityLogEntry(clientId.Value, $"Created Credit Top-up Invoice - Invoice ID: {invoice.Id}"), ct);
        await _context.SaveChangesAsync(ct);

        return Result<int>.Success(invoice.Id);
    }
}
