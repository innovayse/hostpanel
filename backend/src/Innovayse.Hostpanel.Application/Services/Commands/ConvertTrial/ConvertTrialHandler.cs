using Innovayse.Hostpanel.Application.Billing.Commands.RaiseClientRenewalInvoices;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Extensions;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.ConvertTrial;

/// <summary>
/// Converts a running free trial into a paid service: the service is saved as awaiting payment first, then the
/// Billing module raises the invoice of its first paid period, due at the end of the trial's grace.
/// </summary>
/// <remarks>
/// <para>
/// The state is saved before the invoice is raised, so an invoice paid at once (a zero total covered by credit)
/// still finds the trial awaiting payment and completes the conversion. Two conversions at once (a staff convert
/// racing the daily job or the client's upgrade) are settled by the service's concurrency token: the loser reads
/// the service again and, finding it awaiting payment, only asks for the invoice, which Billing raises once.
/// </para>
/// <para>
/// A trial that is to end at the end of its period (an open cancellation request or the automation's
/// end-of-cycle termination) is never converted: the daily job terminates it instead, so it is refused here
/// before anything changes.
/// </para>
/// </remarks>
public sealed class ConvertTrialHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Asks the Billing module for the conversion invoice.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Reads the moment of the conversion.</summary>
    private readonly IClock _clock;

    /// <summary>Logs an invoice the Billing module refused to raise.</summary>
    private readonly ILogger<ConvertTrialHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Asks the Billing module for the conversion invoice.</param>
    /// <param name="clock">Reads the moment of the conversion.</param>
    /// <param name="logger">Logs an invoice the Billing module refused to raise.</param>
    public ConvertTrialHandler(IHostpanelDbContext context, IMessageBus bus, IClock clock, ILogger<ConvertTrialHandler> logger)
    {
        _context = context;
        _bus = bus;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The trial service to convert.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success (also for a trial already awaiting payment, and when Billing refuses the invoice: the daily job
    /// raises it again), or <c>SERVICE_NOT_FOUND</c> when the id names no service, <c>SERVICE_NOT_IN_TRIAL</c>
    /// when its trial is not running, <c>SERVICE_NOT_ACTIVE</c> when a running trial's service is not Active, or
    /// <c>CANCELLATION_ALREADY_PENDING</c> when the trial is to end at the end of its period.
    /// </returns>
    public async Task<Result> HandleAsync(ConvertTrialCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // Tracked, not AsNoTracking: an Active trial is changed and saved below.
        var service = await _context.ClientServices.FirstOrDefaultAsync(s => s.Id == cmd.ServiceId, ct);
        if (service is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        if (!service.IsTrialRunning)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_IN_TRIAL);
        }

        if (service.TrialStatus == TrialStatus.Active && service.Status is not ServiceStatus.Active)
        {
            return Result.FailureCode(HostpanelErrorCodes.SERVICE_NOT_ACTIVE);
        }

        // Billing raises no invoice for such a trial, so it is refused here rather than answered as a silent success.
        if (await _context.EndsAtPeriodEndAsync(service, ct))
        {
            return Result.FailureCode(HostpanelErrorCodes.CANCELLATION_ALREADY_PENDING);
        }

        if (service.TrialStatus == TrialStatus.Active)
        {
            var begun = await BeginConversionAsync(service, ct);
            if (begun.Value is not { } awaiting)
            {
                return begun;
            }

            service = awaiting;
        }

        await RaiseInvoiceAsync(service, ct);
        return Result.Success();
    }

    /// <summary>
    /// Moves an Active trial to awaiting payment and saves it. When another call saved the service first, it is read
    /// again: a trial that call converted goes on to the invoice, anything else is refused.
    /// </summary>
    /// <param name="service">The tracked trial service, Active as a trial and as a service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The service awaiting payment, or <c>SERVICE_NOT_IN_TRIAL</c> when the other call left it in another state.</returns>
    private async Task<Result<ClientService>> BeginConversionAsync(ClientService service, CancellationToken ct)
    {
        service.BeginTrialConversion(_clock.UtcNow);
        try
        {
            await _context.SaveChangesAsync(ct);
            return Result<ClientService>.Success(service);
        }
        catch (ConcurrencyConflictException)
        {
            // The change that lost is dropped with everything tracked, and the service is read as the winner saved it.
            _context.DetachAll();
            var current = await _context.ClientServices.AsNoTracking().FirstOrDefaultAsync(s => s.Id == service.Id, ct);
            return current is { TrialStatus: TrialStatus.AwaitingPayment }
                ? Result<ClientService>.Success(current)
                : Result<ClientService>.FailureCode(HostpanelErrorCodes.SERVICE_NOT_IN_TRIAL);
        }
    }

    /// <summary>
    /// Asks the Billing module for the invoice of the first paid period, due at the end of the grace. A refusal is
    /// logged and not answered: the service already awaits payment, and the daily job raises the invoice again.
    /// </summary>
    /// <param name="service">The service awaiting payment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once Billing has answered.</returns>
    private async Task RaiseInvoiceAsync(ClientService service, CancellationToken ct)
    {
        var raised = await _bus.InvokeAsync<Result<int>>(
            new RaiseClientRenewalInvoicesCommand(service.ClientId, TrialServiceId: service.Id, DueAt: service.TrialPaymentDueAt), ct);
        if (raised.IsFailure)
        {
            _logger.LogWarning(
                "Billing refused the conversion invoice of trial service {ServiceId}: {ErrorCode}.", service.Id, raised.ErrorCode);
        }
    }
}
