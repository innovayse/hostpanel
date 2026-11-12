using Innovayse.Hostpanel.Application.Fraud.Commands.RecordFraudDecision;
using Innovayse.Hostpanel.Application.Fraud.Common;
using Innovayse.Hostpanel.Application.Fraud.Queries.EvaluateFraudRisk;
using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Fraud.Extensions;

/// <summary>The two calls the Orders and Auth modules make to the Fraud module, shaped once so both ask and log the same way.</summary>
public static class FraudBusExtensions
{
    /// <summary>Asks Fraud what to make of a registration or an order.</summary>
    /// <param name="bus">The Wolverine bus to dispatch the query through.</param>
    /// <param name="kind">What is being checked.</param>
    /// <param name="email">The e-mail address, when known.</param>
    /// <param name="billingCountry">The billing country on record, when there is one.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The verdict. Fraud detection never refuses the question, so a failed answer is a bug and throws.</returns>
    /// <exception cref="InvalidOperationException">The Fraud module refused the question.</exception>
    public static async Task<FraudVerdictDto> EvaluateFraudAsync(
        this IMessageBus bus, FraudCheckKind kind, string? email, string? billingCountry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var verdict = await bus.InvokeAsync<Result<FraudVerdictDto>>(new EvaluateFraudRiskQuery(kind, email, billingCountry), ct);
        return verdict.Value ?? throw new InvalidOperationException($"The fraud check was refused with {verdict.ErrorCode}.");
    }

    /// <summary>Writes a verdict to the fraud log. A log line that cannot be written is logged and never fails the request it records.</summary>
    /// <param name="bus">The Wolverine bus to dispatch the command through.</param>
    /// <param name="logger">Where a refused write is logged.</param>
    /// <param name="command">The decision to write.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the line is written or the refusal logged.</returns>
    public static async Task RecordFraudDecisionAsync(
        this IMessageBus bus, ILogger logger, RecordFraudDecisionCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(logger);

        var recorded = await bus.InvokeAsync<Result<int>>(command, ct);
        if (recorded.IsFailure)
        {
            logger.LogWarning("A fraud decision of {Decision} could not be logged: {ErrorCode}.", command.Decision, recorded.ErrorCode);
        }
    }
}
