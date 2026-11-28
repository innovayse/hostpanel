using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.ConvertTrial;
using Innovayse.Hostpanel.Application.Services.Commands.TerminateTrial;
using Innovayse.Hostpanel.Application.Services.Extensions;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.ProcessTrial;

/// <summary>
/// Takes one free trial's daily step, deciding on the trial's own dates and states only (never on the renewal date
/// or the service status alone), first match wins:
/// <list type="bullet">
/// <item>a running trial that has not ended gets the reminder due today, once;</item>
/// <item>a running trial that has ended is terminated when it is to end at the end of its period, when its service
/// is not Active or when the product does not convert it, and converted otherwise;</item>
/// <item>a trial awaiting payment is terminated at once when it is to end at the end of its period, and once its
/// payment is due, unless (in both cases) its conversion invoice is paid; before the due date the conversion is
/// repeated, which only raises a missing invoice.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// The conversion and the termination are the <c>ConvertTrial</c> and <c>TerminateTrial</c> use cases, dispatched
/// on the bus. A reminder is a <see cref="TrialReminder"/> row saved together with the mail its event queues, so the
/// unique index on the service and the reminder day makes it once-only even when two runs race.
/// </para>
/// <para>
/// The daily job invokes this command inline (<c>InvokeAsync</c>, one trial after another, not over a queue) and
/// logs a failed result as a refused step, so a rule or a lost race that stops the conversion or the termination is
/// answered as a result code instead of thrown; the next day's run takes the step again.
/// </para>
/// </remarks>
public sealed class ProcessTrialHandler
{
    /// <summary>The reason passed to the panel when a trial that was to end at the end of its period is terminated.</summary>
    private const string CancelledReason = "Free trial cancelled";

    /// <summary>The reason passed to the panel when an ended trial is not converted.</summary>
    private const string EndedReason = "Free trial ended";

    /// <summary>The reason passed to the panel when a converted trial's invoice was not paid within the grace.</summary>
    private const string NotPaidReason = "Free trial not paid";

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Dispatches the conversion and the termination, and reads the conversion invoice from Billing.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Reads the moment of the step.</summary>
    private readonly IClock _clock;

    /// <summary>Logs a trial left alone because its conversion invoice is paid.</summary>
    private readonly ILogger<ProcessTrialHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Dispatches the conversion and the termination, and reads the conversion invoice from Billing.</param>
    /// <param name="clock">Reads the moment of the step.</param>
    /// <param name="logger">Logs a trial left alone because its conversion invoice is paid.</param>
    public ProcessTrialHandler(IHostpanelDbContext context, IMessageBus bus, IClock clock, ILogger<ProcessTrialHandler> logger)
    {
        _context = context;
        _bus = bus;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The trial service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, also when there was nothing to do (the id names no service, or its trial is not running); otherwise
    /// the result of the conversion or the termination, or the code of a rule or a lost race that stopped it.
    /// </returns>
    public async Task<Result> HandleAsync(ProcessTrialCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var service = await _context.ClientServices.AsNoTracking().FirstOrDefaultAsync(s => s.Id == cmd.ServiceId, ct);
        if (service is null)
        {
            return Result.Success();
        }

        var now = _clock.UtcNow;
        return service.TrialStatus switch
        {
            TrialStatus.Active => await StepActiveAsync(service, now, ct),
            TrialStatus.AwaitingPayment => await StepAwaitingPaymentAsync(service, now, ct),
            _ => Result.Success(),
        };
    }

    /// <summary>Takes the step of a running trial: its reminder before the end, its conversion or termination after.</summary>
    /// <param name="service">The trial service, read without tracking.</param>
    /// <param name="now">The moment of the step.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The step's result.</returns>
    private async Task<Result> StepActiveAsync(ClientService service, DateTimeOffset now, CancellationToken ct)
    {
        if (service.TrialEndsAt is not { } endsAt || endsAt > now)
        {
            return await SendDueReminderAsync(service, now, ct);
        }

        if (await _context.EndsAtPeriodEndAsync(service, ct))
        {
            return await DispatchAsync(new TerminateTrialCommand(service.Id, CancelledReason), ct);
        }

        // Nothing ran (a hosting trial never set up) or the product does not convert: there is nothing to bill.
        if (service.Status is not ServiceStatus.Active || !service.TrialAutoConvert)
        {
            return await DispatchAsync(new TerminateTrialCommand(service.Id, EndedReason), ct);
        }

        return await DispatchAsync(new ConvertTrialCommand(service.Id), ct);
    }

    /// <summary>
    /// Takes the step of a trial awaiting payment: terminated at once when it is to end at the end of its period, and
    /// at the due date, unless its invoice is paid; before the due date its conversion is repeated (a missing invoice
    /// is raised again).
    /// </summary>
    /// <param name="service">The trial service, read without tracking.</param>
    /// <param name="now">The moment of the step.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The step's result.</returns>
    private async Task<Result> StepAwaitingPaymentAsync(ClientService service, DateTimeOffset now, CancellationToken ct)
    {
        if (await _context.EndsAtPeriodEndAsync(service, ct))
        {
            if (await IsConversionInvoicePaidAsync(service, ct))
            {
                // The invoice is paid (or its payment is on its way): the payment completes the conversion and the
                // cancellation is then handled as for any paid service. Terminating now would only be refused, every
                // day, until the payment is applied.
                _logger.LogInformation(
                    "Trial service {ServiceId} is to end at its period end but its conversion invoice is paid; left for the renewal sweep.",
                    service.Id);
                return Result.Success();
            }

            return await DispatchAsync(new TerminateTrialCommand(service.Id, CancelledReason), ct);
        }

        if (service.TrialPaymentDueAt is not { } dueAt || dueAt > now)
        {
            return await DispatchAsync(new ConvertTrialCommand(service.Id), ct);
        }

        if (await IsConversionInvoicePaidAsync(service, ct))
        {
            // The client paid (or a payment is on its way) and the renewal sweep has not applied it yet: it completes
            // the conversion, so the service is kept.
            _logger.LogInformation(
                "Trial service {ServiceId} is past its payment due date but its conversion invoice is paid; left for the renewal sweep.",
                service.Id);
            return Result.Success();
        }

        return await DispatchAsync(new TerminateTrialCommand(service.Id, NotPaidReason), ct);
    }

    /// <summary>
    /// Records the reminder due now when it was not sent yet; its event mails the client in the same save. A second
    /// run that saved the same reminder first is answered as already sent.
    /// </summary>
    /// <param name="service">The trial service, read without tracking.</param>
    /// <param name="now">The moment of the step.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success: the reminder was recorded, was already sent, or none is due.</returns>
    private async Task<Result> SendDueReminderAsync(ClientService service, DateTimeOffset now, CancellationToken ct)
    {
        if (service.DueTrialReminder(now) is not { } daysBefore)
        {
            return Result.Success();
        }

        var sent = await _context.TrialReminders.AnyAsync(r => r.ServiceId == service.Id && r.DaysBefore == daysBefore, ct);
        if (sent)
        {
            return Result.Success();
        }

        _context.TrialReminders.Add(TrialReminder.Create(service, daysBefore, now));
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // The unique index refused a second row: another run recorded (and mailed) this reminder first.
            _context.DetachAll();
        }

        return Result.Success();
    }

    /// <summary>
    /// Whether the conversion invoice of a trial awaiting payment is paid (also partly refunded since), or its payment is
    /// pending.
    /// </summary>
    /// <param name="service">The trial service awaiting payment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when Billing has the invoice of the first paid period and it is
    /// <see cref="ServiceRenewalInvoiceDto.IsPaidOrPaymentPending"/>.
    /// </returns>
    private async Task<bool> IsConversionInvoicePaidAsync(ClientService service, CancellationToken ct)
    {
        if (service.NextRenewalAt is not { } start)
        {
            return false;
        }

        var invoice = await _bus.GetServiceRenewalInvoiceAsync(service.Id, start, ct);
        return invoice is { IsPaidOrPaymentPending: true };
    }

    /// <summary>
    /// Dispatches the conversion or the termination and answers its result; a rule or a lost race that stops it is
    /// answered as a code, so the daily job logs it as a refused step instead of a failure.
    /// </summary>
    /// <param name="command">The <see cref="ConvertTrialCommand"/> or <see cref="TerminateTrialCommand"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The use case's result, or the code of the exception that stopped it.</returns>
    private async Task<Result> DispatchAsync(object command, CancellationToken ct)
    {
        try
        {
            return await _bus.InvokeAsync<Result>(command, ct);
        }
        catch (DomainRuleException ex)
        {
            return Result.FailureCode(ex.Code);
        }
        catch (ConcurrencyConflictException)
        {
            return Result.FailureCode(SharedErrorCodes.Conflict);
        }
    }
}
