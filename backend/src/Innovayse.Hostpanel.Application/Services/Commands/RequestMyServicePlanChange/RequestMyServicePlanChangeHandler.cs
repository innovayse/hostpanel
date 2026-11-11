using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Billing.Commands.CancelInvoice;
using Innovayse.Hostpanel.Application.Billing.Commands.CreateInvoice;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.CompleteServiceChange;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.RequestMyServicePlanChange;

/// <summary>
/// Records a plan change request against one of the calling client's own services, refusing every service that is not
/// theirs, and moves it on as far as it can go without staff.
/// </summary>
/// <remarks>
/// <para>
/// An upgrade is approved at once. The prorated charge becomes an invoice through the one invoice creation use case, the
/// request keeps the invoice id, and the plan changes when that invoice is paid (<c>ServiceChangeInvoicePaidHandler</c>);
/// an upgrade with nothing left to prorate has no invoice and is applied now. When the save that links the invoice to
/// the request fails, the invoice is cancelled and the request taken back, so no payable invoice is left for a change that
/// is never applied. The request is saved before the invoice is raised and approved with it, so only the request that
/// wins the unique index on open requests ever raises one. A downgrade is approved at once only when
/// the <c>billing.plan_change.auto_approve_downgrades</c> setting is on, otherwise it waits as Pending for staff. An approved
/// change is applied by <see cref="CompleteServiceChangeCommand"/>, the same use case staff approval ends in.
/// </para>
/// <para>
/// Ownership is settled before anything else, so a stranger's id never reaches a query that would answer differently for
/// an id that exists. A failed invoice takes the saved request back, so the
/// service is never left holding an open request that waits for an invoice that does not exist. Known limits: the proration
/// is worked out from list prices, a downgrade quoted earlier keeps its amount until staff decide it, and a request whose
/// invoice is cancelled stays Approved until staff reject it.
/// </para>
/// </remarks>
public sealed class RequestMyServicePlanChangeHandler
{
    /// <summary>How long the proration invoice stays payable before its due date, in days.</summary>
    private const int InvoiceDueInDays = 7;

    /// <summary>The rule that says a client may only touch their own services.</summary>
    private readonly IServiceOwnership _ownership;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Works out which plans the service can move to and what the move costs.</summary>
    private readonly ServicePlanChangeQuoter _quoter;

    /// <summary>Reads whether a downgrade is approved without staff.</summary>
    private readonly ServicePlanChangeSettingsReader _settings;

    /// <summary>The clock the moment of the request is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Records the activity log line for this action, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Wolverine bus, used to raise the invoice and to apply the change through their own use cases.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Reports an invoice or a request that could not be withdrawn.</summary>
    private readonly ILogger<RequestMyServicePlanChangeHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="ownership">The rule that says a client may only touch their own services.</param>
    /// <param name="context">The module's data context.</param>
    /// <param name="quoter">Works out which plans the service can move to and what the move costs.</param>
    /// <param name="settings">Reads whether a downgrade is approved without staff.</param>
    /// <param name="clock">The clock the moment of the request is read from.</param>
    /// <param name="activity">Records the activity log line for this action, saved with the change it describes.</param>
    /// <param name="bus">Wolverine bus, used to raise the invoice and to apply the change through their own use cases.</param>
    /// <param name="logger">Reports an invoice or a request that could not be withdrawn.</param>
    public RequestMyServicePlanChangeHandler(
        IServiceOwnership ownership,
        IHostpanelDbContext context,
        ServicePlanChangeQuoter quoter,
        ServicePlanChangeSettingsReader settings,
        IClock clock,
        IActivityLogger activity,
        IMessageBus bus,
        ILogger<RequestMyServicePlanChangeHandler> logger)
    {
        _ownership = ownership;
        _context = context;
        _quoter = quoter;
        _settings = settings;
        _clock = clock;
        _activity = activity;
        _bus = bus;
        _logger = logger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The command. It names no account: this acts on the caller's own service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Where the request stands, or <c>SERVICE_NOT_FOUND</c> when the service is not the caller's, <c>CLIENT_SUSPENDED</c> or
    /// <c>CLIENT_CLOSED</c> when the account may not transact, <c>SERVICE_CHANGE_ALREADY_PENDING</c> when an open request exists,
    /// any refusal of <see cref="ServicePlanChangeQuoter.QuoteAsync"/>, the invoice use case's failure, or the failure of
    /// applying an approved change; the request stays Approved for staff to retry in that last case.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    /// <exception cref="Exception">The failure of the save that records the request, rethrown after its invoice was withdrawn.</exception>
    public async Task<Result<ServicePlanChangeResultDto>> HandleAsync(RequestMyServicePlanChangeCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(cmd.ServiceId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return Result<ServicePlanChangeResultDto>.FailureCode(ownershipOutcome.ErrorCode!);
        }

        var service = await _context.ClientServices.AsNoTracking().FirstOrDefaultAsync(s => s.Id == cmd.ServiceId, ct);
        if (service is null)
        {
            return Result<ServicePlanChangeResultDto>.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        var mayTransact = await _bus.EnsureClientMayTransactAsync(service.ClientId, ct);
        if (mayTransact.IsFailure)
        {
            return Result<ServicePlanChangeResultDto>.FailureCode(mayTransact.ErrorCode!);
        }

        // A pending or an approved request is still open: the second would charge or credit the same cycle twice.
        var hasOpenRequest = await _context.ServiceChangeRequests.AnyAsync(
            r => r.ServiceId == service.Id
                 && (r.Status == ServiceChangeStatus.Pending || r.Status == ServiceChangeStatus.Approved),
            ct);
        if (hasOpenRequest)
        {
            return Result<ServicePlanChangeResultDto>.FailureCode(HostpanelErrorCodes.SERVICE_CHANGE_ALREADY_PENDING);
        }

        var quote = await _quoter.QuoteAsync(service, cmd.ToProductId, ct);
        if (quote.IsFailure)
        {
            return Result<ServicePlanChangeResultDto>.FailureCode(quote.ErrorCode!);
        }

        var option = quote.Value!;
        var request = ServiceChangeRequest.Create(
            service.Id,
            service.ClientId,
            service.ProductId,
            option.ProductId,
            new ProratedPlanChange(option.Type, option.ProrationAmount),
            option.CurrencyCode,
            option.NewRecurringAmount,
            _clock.UtcNow);

        var approvedAtOnce = option.Type is ServiceChangeType.Upgrade || await _settings.AutoApproveDowngradesAsync(ct);
        var needsInvoice = approvedAtOnce && option.Type is ServiceChangeType.Upgrade && option.ProrationAmount > 0m;

        // A request that waits for an invoice is saved Pending first: the unique index on open requests settles a race before
        // any invoice exists, so the loser never raises one.
        if (approvedAtOnce && !needsInvoice)
        {
            request.Approve();
        }

        _context.ServiceChangeRequests.Add(request);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // A request raised for the same service at the same moment was saved first: the unique index on open requests
            // refused this one, which is the open request the check above looks for.
            _context.DetachAll();
            return Result<ServicePlanChangeResultDto>.FailureCode(HostpanelErrorCodes.SERVICE_CHANGE_ALREADY_PENDING);
        }

        if (needsInvoice)
        {
            var invoice = await RaiseInvoiceAsync(service, option, ct);
            if (invoice.IsFailure)
            {
                // The request holds the service's one open slot and waits for an invoice that does not exist: take it back.
                await RemoveRequestAsync(request);
                return Result<ServicePlanChangeResultDto>.FailureCode(invoice.ErrorCode!);
            }

            request.Approve();
            request.AttachInvoice(invoice.Value);
        }

        try
        {
            await _activity.RecordAsync(
                new ActivityLogEntry(
                    service.ClientId,
                    $"Requested {option.Type} - Service ID: {service.Id}, Request ID: {request.Id}, New Product ID: {option.ProductId}"),
                ct);
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception) when (request.InvoiceId is not null)
        {
            // The invoice is payable but the request that would apply it was not saved as linked to it: withdraw both. Not the
            // request's token, since a cancelled request is exactly when the invoice must still be withdrawn. The original
            // failure is what the caller sees.
            var invoiceId = request.InvoiceId.Value;
            var cancelled = await _bus.InvokeAsync<Result>(new CancelInvoiceCommand(invoiceId), CancellationToken.None);
            if (cancelled.IsFailure)
            {
                _logger.LogCritical(
                    "Plan change save failed and its invoice {InvoiceId} could not be cancelled: {Code}. Cancel it by hand.",
                    invoiceId, cancelled.ErrorCode);
            }

            await TryRemoveRequestAsync(request);
            throw;
        }

        // An approved change with no invoice to wait for is applied now. A failure leaves the request Approved for staff.
        if (approvedAtOnce && !needsInvoice)
        {
            var completed = await _bus.InvokeAsync<Result>(new CompleteServiceChangeCommand(request.Id), ct);
            if (completed.IsFailure)
            {
                return Result<ServicePlanChangeResultDto>.FailureCode(completed.ErrorCode!);
            }
        }

        var status = await _context.ServiceChangeRequests
            .AsNoTracking()
            .Where(r => r.Id == request.Id)
            .Select(r => r.Status)
            .FirstAsync(ct);
        return Result<ServicePlanChangeResultDto>.Success(new ServicePlanChangeResultDto(
            request.Id, option.Type, status, option.ProrationAmount, option.CurrencyCode, request.InvoiceId));
    }

    /// <summary>Takes a saved request back so the service's one open slot is free again.</summary>
    /// <param name="request">The saved request.</param>
    /// <returns>A task that completes once the request is deleted.</returns>
    private async Task RemoveRequestAsync(ServiceChangeRequest request)
    {
        // The context may still hold the change set that failed: discard it so only the delete is saved, and ignore the
        // request's token for the same reason as the invoice cancellation.
        _context.DiscardPendingChanges();
        _context.ServiceChangeRequests.Remove(request);
        await _context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Takes a saved request back after another failure, without letting a failure here hide the original one.</summary>
    /// <param name="request">The saved request.</param>
    /// <returns>A task that completes once the request is deleted or the failure to delete it is logged.</returns>
    private async Task TryRemoveRequestAsync(ServiceChangeRequest request)
    {
        try
        {
            await RemoveRequestAsync(request);
        }
        catch (Exception removal)
        {
            _logger.LogCritical(removal, "Plan change request {RequestId} could not be removed after its save failed. Remove it by hand.", request.Id);
        }
    }

    /// <summary>Raises the invoice of the prorated charge through the one invoice creation use case.</summary>
    /// <param name="service">The service that changes plan.</param>
    /// <param name="option">The quote, naming the new plan and the amount to charge.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new invoice's id, or the invoice use case's failure.</returns>
    private async Task<Result<int>> RaiseInvoiceAsync(ClientService service, ServicePlanChangeOptionDto option, CancellationToken ct)
    {
        // An invoice line is a stored record of what was sold, not a screen: like every other line the billing flows write,
        // it is one fixed sentence around the product name rather than a resource looked up per language.
        var item = new CreateInvoiceItemDto($"Upgrade to {option.Name} (prorated for the rest of the billing period)", option.ProrationAmount, 1);
        return await _bus.InvokeAsync<Result<int>>(
            new CreateInvoiceCommand(service.ClientId, _clock.UtcNow.AddDays(InvoiceDueInDays), [item]), ct);
    }
}
