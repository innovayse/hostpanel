using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Orders.Commands.ClaimOrderLines;
using Innovayse.Hostpanel.Application.Orders.Commands.RefreshOrderStatus;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Orders.Extensions;

/// <summary>
/// Sends the commands that change one order line's state, and sends them again when a concurrent save won the race.
/// </summary>
/// <remarks>
/// Each handler loads the order afresh and answers <c>ORDER_LINE_CHANGED</c> when another writer saved the order or the
/// line between its load and its save, having written nothing; sending the command again loads what the winner saved
/// and applies the change to that. A line's state is a fact (money moved, a service exists), so a change is retried
/// rather than dropped.
/// </remarks>
public static class OrderLineBusExtensions
{
    /// <summary>How many times a command that keeps meeting a concurrent save is sent before its answer is returned.</summary>
    private const int MaxAttempts = 3;

    /// <summary>
    /// Sends a command to a handler that answers <c>ORDER_LINE_CHANGED</c> on a lost race, again while it does, up to
    /// <see cref="MaxAttempts"/> times.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <param name="bus">The message bus.</param>
    /// <param name="command">The command to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The first answer that is not <c>ORDER_LINE_CHANGED</c>, or the last answer when every attempt lost the race.</returns>
    public static async Task<Result> InvokeLineChangeAsync<TCommand>(this IMessageBus bus, TCommand command, CancellationToken ct)
        where TCommand : notnull
    {
        ArgumentNullException.ThrowIfNull(bus);

        var result = Result.FailureCode(HostpanelErrorCodes.ORDER_LINE_CHANGED);
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            result = await bus.InvokeAsync<Result>(command, ct);
            if (result.IsSuccess || result.ErrorCode != HostpanelErrorCodes.ORDER_LINE_CHANGED)
            {
                return result;
            }
        }

        return result;
    }

    /// <summary>
    /// Brings an order's status in line with its lines as the database holds them now, sending the refresh again when
    /// a concurrent save won the race.
    /// </summary>
    /// <param name="bus">The message bus.</param>
    /// <param name="orderId">The order.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success once the status fits the lines, or the refusal that stopped it.</returns>
    public static Task<Result> RefreshOrderStatusAsync(this IMessageBus bus, int orderId, CancellationToken ct)
    {
        return bus.InvokeLineChangeAsync(new RefreshOrderStatusCommand(orderId), ct);
    }

    /// <summary>
    /// Claims an order's lines for a run, sending the claim again while a concurrent save wins the race, up to
    /// <see cref="MaxAttempts"/> times.
    /// </summary>
    /// <param name="bus">The message bus.</param>
    /// <param name="command">The order and, optionally, the one line to claim.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The lines the claim gave this run with their attempts, or the refusal that stopped it.</returns>
    public static async Task<Result<IReadOnlyList<ClaimedOrderLineDto>>> ClaimOrderLinesAsync(
        this IMessageBus bus, ClaimOrderLinesCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);

        var result = Result<IReadOnlyList<ClaimedOrderLineDto>>.FailureCode(HostpanelErrorCodes.ORDER_LINE_CHANGED);
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            result = await bus.InvokeAsync<Result<IReadOnlyList<ClaimedOrderLineDto>>>(command, ct);
            if (result.IsSuccess || result.ErrorCode != HostpanelErrorCodes.ORDER_LINE_CHANGED)
            {
                return result;
            }
        }

        return result;
    }
}
