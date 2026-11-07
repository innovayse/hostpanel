using Innovayse.Hostpanel.Application.Billing.Commands.ChargeInvoiceStoredMethod;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ChargeClientRenewalInvoices;

/// <summary>
/// Handles <see cref="ChargeClientRenewalInvoicesCommand"/>: charges each open renewal invoice of a client that has
/// no charge in flight through <see cref="ChargeInvoiceStoredMethodCommand"/>.
/// </summary>
/// <remarks>
/// <para>
/// The fallback is the existing flow. A client with no active consent is skipped before any invoice is read, and a
/// charge that is refused (no capability, no stored method, a decline) leaves the invoice unpaid, so the invoice and
/// the pay link go out as they always did. The invoice's own checks (payable, account may transact, claim gate)
/// are made by the charge command, not repeated here.
/// </para>
/// <para>
/// A failed charge is not retried on every pass. After a declined charge, or one the gateway confirmed it never made,
/// the invoice waits <see cref="StoredChargeOptions.RetryAfterDays"/> days from the end of the last attempt, and after
/// <see cref="StoredChargeOptions.MaxAttemptsPerInvoice"/> attempts it is left unpaid for the usual flow. An invoice
/// with a claim in flight, or one held for an operator, is skipped. Each declined attempt mails the client once (the
/// mail is keyed by the claim), so the waits are also what keeps the client from being mailed on every pass.
/// </para>
/// </remarks>
public sealed class ChargeClientRenewalInvoicesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The Wolverine bus the charge command is dispatched through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The wait after a failed attempt and the attempts an invoice gets.</summary>
    private readonly IOptions<StoredChargeOptions> _options;

    /// <summary>The clock the back-off is measured on.</summary>
    private readonly IClock _clock;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<ChargeClientRenewalInvoicesHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The Wolverine bus the charge command is dispatched through.</param>
    /// <param name="options">The wait after a failed attempt and the attempts an invoice gets.</param>
    /// <param name="clock">The clock the back-off is measured on.</param>
    /// <param name="logger">Structured logger.</param>
    public ChargeClientRenewalInvoicesHandler(
        IHostpanelDbContext context,
        IMessageBus bus,
        IOptions<StoredChargeOptions> options,
        IClock clock,
        ILogger<ChargeClientRenewalInvoicesHandler> logger)
    {
        _context = context;
        _bus = bus;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of invoices a charge was started for (whatever its outcome).</returns>
    public async Task<Result<int>> HandleAsync(ChargeClientRenewalInvoicesCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var hasConsent = await _context.CardChargeConsents
            .AsNoTracking()
            .AnyAsync(c => c.ClientId == cmd.ClientId && c.MethodRef != null && c.RevokedAt == null, ct);
        if (!hasConsent)
        {
            return Result.Success(0);
        }

        // Invoices that already have a charge in flight, or one held for an operator, are left alone: the charge
        // command would refuse them anyway.
        var claimed = await _context.InvoicePaymentClaims
            .AsNoTracking()
            .Where(c => c.ClientId == cmd.ClientId)
            .Where(InvoicePaymentClaim.BlockingFilter)
            .Select(c => c.InvoiceId)
            .ToListAsync(ct);

        // Only renewal invoices (a line that renews a period) that are open for payment.
        var invoiceIds = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.ClientId == cmd.ClientId)
            .Where(Invoice.PayableFilter)
            .Where(i => i.Items.Any(l => l.RenewalPeriodStart != null) && !claimed.Contains(i.Id))
            .OrderBy(i => i.Id)
            .Select(i => i.Id)
            .ToListAsync(ct);

        var backedOff = await InvoicesToWaitAsync(cmd.ClientId, ct);

        var started = 0;
        foreach (var invoiceId in invoiceIds)
        {
            if (backedOff.Contains(invoiceId))
            {
                _logger.LogInformation(
                    "Stored charge of renewal invoice {InvoiceId} waits: its last attempt failed recently or it used all its attempts.", invoiceId);
                continue;
            }

            try
            {
                var charged = await _bus.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId), ct);
                if (charged.IsFailure)
                {
                    _logger.LogInformation(
                        "Stored charge of renewal invoice {InvoiceId} was not made ({Code}); the invoice stays unpaid for the usual flow.", invoiceId, charged.ErrorCode);
                    continue;
                }

                started++;
            }
            catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
            {
                _logger.LogError(ex, "Stored charge of renewal invoice {InvoiceId} failed; the next run retries it.", invoiceId);
            }
        }

        return Result.Success(started);
    }

    /// <summary>
    /// Finds the client's invoices the run must not charge now: those whose last declined or abandoned attempt ended less
    /// than <see cref="StoredChargeOptions.RetryAfterDays"/> days ago, and those that already had
    /// <see cref="StoredChargeOptions.MaxAttemptsPerInvoice"/> failed attempts. A claim abandoned before the plugin was
    /// called is not an attempt (<see cref="InvoicePaymentClaim.FailedAttemptFilter"/>).
    /// </summary>
    /// <param name="clientId">The client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ids of the invoices to leave for a later pass or for the usual flow.</returns>
    private async Task<HashSet<int>> InvoicesToWaitAsync(int clientId, CancellationToken ct)
    {
        var options = _options.Value;
        var cutoff = _clock.UtcNow - options.RetryAfter;

        var failed = await _context.InvoicePaymentClaims
            .AsNoTracking()
            .Where(c => c.ClientId == clientId)
            .Where(InvoicePaymentClaim.FailedAttemptFilter)
            .GroupBy(c => c.InvoiceId)
            .Select(g => new { InvoiceId = g.Key, Attempts = g.Count(), LastEnded = g.Max(c => c.ResolvedAt ?? c.CreatedAt) })
            .ToListAsync(ct);

        return failed
            .Where(f => f.Attempts >= options.MaxAttemptsPerInvoice || f.LastEnded > cutoff)
            .Select(f => f.InvoiceId)
            .ToHashSet();
    }
}
