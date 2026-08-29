namespace Innovayse.API;

using Innovayse.Application.Billing.Common;
using Innovayse.Application.Clients.Common;
using Innovayse.Application.Support.Common;
using Serilog.Core;
using Serilog.Events;

/// <summary>
/// Drops the one Error-level line Wolverine writes when a handler refuses a request by throwing
/// a control-flow exception, so the only record of that refusal is the single Information line
/// <see cref="ExceptionMiddleware"/> writes when it answers 404.
/// <para>
/// <b>Why this is a log filter and not Wolverine configuration.</b> Wolverine's
/// <c>Executor.InvokeAsync</c> (and its <c>TracingExecutor</c> twin) opens its catch block with
/// an unconditional <c>_logger.LogError(e, "Invocation of {Message} failed!", …)</c>, emitted
/// <i>before</i> any failure rule is consulted. Nothing in WolverineFx 5.31.0 guards it: the
/// error-handling policies (<c>OnException&lt;T&gt;().Discard()</c> and friends) only decide
/// what happens after the line is already written, <c>Policies.MessageExecutionLogLevel</c> and
/// <c>MessageSuccessLogLevel</c> address the start/stop and success lines rather than this one,
/// and <c>InvokeTracing</c> only chooses between the two executors that both write it. Its
/// logger category is the message type, so silencing the category would silence real faults in
/// the same handler. The event therefore has to be recognised where it lands, in the sink.
/// </para>
/// <para>
/// <b>Why this cannot hide a genuine failure.</b> Both conditions below must hold: the event
/// must carry Wolverine's exact message template, and its exception must be <i>exactly</i> one
/// of the three refusal types. A handler that throws anything else — including a real fault
/// whose inner exception happens to be one of these — still reaches the sink at Error with its
/// stack trace, as does <see cref="ExceptionMiddleware"/>'s own "Unhandled exception" line,
/// which carries a different template.
/// </para>
/// <para>
/// <b>How it fails.</b> If a Wolverine upgrade rewords that template, the filter stops matching
/// and the noise comes back — it never starts swallowing something new. That is the intended
/// direction to fail in, and it is why the template is matched literally rather than loosely.
/// </para>
/// <para>
/// Registered as a singleton in the composition root and picked up by Serilog's
/// <c>ReadFrom.Services()</c>, which collects every <see cref="ILogEventFilter"/> in the
/// container. The Testing environment does not configure Serilog at all, so this is inert
/// there.
/// </para>
/// </summary>
public sealed class ControlFlowExceptionLogFilter : ILogEventFilter
{
    /// <summary>
    /// Wolverine's message template for the line this filter exists to remove, copied verbatim
    /// from <c>Wolverine.Runtime.Handlers.Executor.InvokeAsync</c> in WolverineFx 5.31.0.
    /// Matching the template rather than the rendered text keeps the check independent of which
    /// message was being invoked.
    /// </summary>
    private const string WolverineInvocationFailedTemplate = "Invocation of {Message} failed!";

    /// <inheritdoc />
    public bool IsEnabled(LogEvent logEvent)
    {
        // Cheapest discriminators first; almost every event leaves on one of these three lines.
        if (logEvent.Level != LogEventLevel.Error) return true;
        if (logEvent.Exception is null) return true;
        if (logEvent.MessageTemplate.Text != WolverineInvocationFailedTemplate) return true;

        return !IsRefusal(logEvent.Exception);
    }

    /// <summary>
    /// Whether the exception is one of the refusals a healthy system throws as a matter of
    /// course: a staff identity that was never onboarded as a customer, or a caller asking for a
    /// ticket or invoice that is not theirs.
    /// </summary>
    /// <param name="exception">The exception Wolverine caught.</param>
    /// <returns>
    /// <c>true</c> when the exception is exactly one of the refusal types, so its Error line may
    /// be dropped; <c>false</c> for everything else, which must stay.
    /// </returns>
    private static bool IsRefusal(Exception exception) =>
        // Exact type, deliberately: an exception that merely *wraps* one of these is a fault
        // that happened while refusing, and losing its stack trace is the outcome this whole
        // filter is written to avoid.
        exception is ClientProfileNotFoundException
            or TicketNotFoundException
            or InvoiceNotFoundException;
}
