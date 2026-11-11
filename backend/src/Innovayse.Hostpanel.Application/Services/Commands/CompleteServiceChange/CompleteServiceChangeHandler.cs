using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetInvoiceBrief;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Queries.GetProductsByIds;
using Innovayse.Hostpanel.Application.Provisioning.Commands.ChangePackage;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.CompleteServiceChange;

/// <summary>
/// Applies an approved plan change request: asks the hosting panel for the new package, moves the service to the new
/// plan at the new price, credits a downgrade's prorated amount to the client's balance and marks the request Completed.
/// Both the automatic approval and a staff approval end here, and so does the payment of an upgrade's invoice.
/// </summary>
/// <remarks>
/// <para>
/// The steps are ordered so a failure leaves nothing half done. The panel is asked first and nothing is staged before it
/// answers, so a panel that refuses leaves the request Approved with no plan changed and no credit given. Everything the
/// client can see (the plan, the price, the credit, the request's status, the activity line) is then staged and saved in
/// one unit of work: a failed save loses all of it together, and the request is still Approved, so the retry asks the panel
/// for the same package again, which changes nothing, and gives the credit once. The request's concurrency token makes the
/// second of two simultaneous runs fail its save rather than credit twice.
/// </para>
/// <para>
/// An upgrade whose proration invoice is not paid in full is refused before anything is done.
/// </para>
/// </remarks>
public sealed class CompleteServiceChangeHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, used to ask the Products and Billing modules and to reach the provisioning use case.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Puts a downgrade's credit on the client's balance in the same save as the plan change.</summary>
    private readonly IClientCredit _clientCredit;

    /// <summary>Records the activity log line for this action, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>The clock the completion moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Wolverine bus, used to ask the Products and Billing modules and to reach the provisioning use case.</param>
    /// <param name="clientCredit">Puts a downgrade's credit on the client's balance in the same save as the plan change.</param>
    /// <param name="activity">Records the activity log line for this action, saved with the change it describes.</param>
    /// <param name="clock">The clock the completion moment is read from.</param>
    public CompleteServiceChangeHandler(
        IHostpanelDbContext context, IMessageBus bus, IClientCredit clientCredit, IActivityLogger activity, IClock clock)
    {
        _context = context;
        _bus = bus;
        _clientCredit = clientCredit;
        _activity = activity;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The approved request to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success (also for a request that is already Completed, so a replayed payment event is harmless), or
    /// <c>SERVICE_CHANGE_REQUEST_NOT_FOUND</c>, <c>SERVICE_CHANGE_INVALID_STATE</c> when the request is not Approved or the
    /// service has left the plan it was requested from, <c>SERVICE_NOT_FOUND</c>, <c>SERVICE_NOT_ACTIVE</c>,
    /// <c>SERVICE_CHANGE_INVOICE_UNPAID</c>, <c>PRODUCT_NOT_FOUND</c>, <c>PRODUCT_PACKAGE_MISSING</c> when a provisioned
    /// service's new plan names no hosting package, or the provisioning use case's failure, for example
    /// <c>SERVICE_PACKAGE_UPSTREAM_FAILED</c>; the request stays Approved in each failure.
    /// </returns>
    public async Task<Result> HandleAsync(CompleteServiceChangeCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var request = await _context.ServiceChangeRequests.FirstOrDefaultAsync(r => r.Id == cmd.RequestId, ct);
        if (request is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_CHANGE_REQUEST_NOT_FOUND);
        }

        if (request.Status is ServiceChangeStatus.Completed)
        {
            return Result.Success();
        }

        if (request.Status is not ServiceChangeStatus.Approved)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_CHANGE_INVALID_STATE);
        }

        var service = await _context.ClientServices.FirstOrDefaultAsync(s => s.Id == request.ServiceId, ct);
        if (service is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        // Staff may have moved the service since the request was made; the amounts were worked out for the old plan.
        if (service.ProductId != request.FromProductId)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_CHANGE_INVALID_STATE);
        }

        if (service.Status is not ServiceStatus.Active)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_ACTIVE);
        }

        if (request.InvoiceId is { } invoiceId && !await IsInvoicePaidAsync(invoiceId, ct))
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_CHANGE_INVOICE_UNPAID);
        }

        var products = await _bus.InvokeAsync<Result<IReadOnlyList<ProductDto>>>(
            new GetProductsByIdsQuery([request.ToProductId]), ct);
        var target = products.Value?.FirstOrDefault();
        if (target is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.PRODUCT_NOT_FOUND);
        }

        // A service never provisioned has no account on a panel to change; its plan is only a record.
        if (service.HoldsHostingAccount)
        {
            if (string.IsNullOrWhiteSpace(target.PackageName))
            {
                return Result.FailureCode(HostpanelErrorCodes.PRODUCT_PACKAGE_MISSING);
            }

            var panel = await _bus.InvokeAsync<Result>(new ChangePackageCommand(service.Id, target.PackageName), ct);
            if (panel.IsFailure)
            {
                return panel;
            }
        }

        if (request.Type is ServiceChangeType.Downgrade && request.ProrationAmount > 0m)
        {
            var credited = await _clientCredit.AddAsync(
                new ClientCreditChange(request.ClientId, request.ProrationAmount, CreditMovementType.PlanChangeCredit), ct);
            if (credited.IsFailure)
            {
                return Result.FailureCode(credited.ErrorCode!);
            }
        }

        service.ChangePlan(request.ToProductId, request.NewRecurringAmount);
        request.Complete(_clock.UtcNow);

        await _activity.RecordAsync(
            new ActivityLogEntry(
                request.ClientId,
                $"Completed {request.Type} - Service ID: {service.Id}, Request ID: {request.Id}, New Product ID: {request.ToProductId}"),
            ct);
        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }

    /// <summary>Whether the proration invoice of an upgrade has been paid in full.</summary>
    /// <param name="invoiceId">The invoice the request waits for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the Billing module reports the invoice paid in full.</returns>
    private async Task<bool> IsInvoicePaidAsync(int invoiceId, CancellationToken ct)
    {
        var brief = await _bus.InvokeAsync<Result<InvoiceBriefDto>>(new GetInvoiceBriefQuery(invoiceId), ct);
        return brief.Value is { IsPaidInFull: true };
    }
}
