using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetClientPluginSlot;

/// <summary>
/// Handles <see cref="GetClientPluginSlotQuery"/>. The order is the contract, unchanged since #644: the slot, then the
/// caller's account, then the context (a service must be the caller's own), and only then the index is built, so nothing
/// is listed for a context the caller may not see and a foreign service id cannot be told from a missing one.
/// </summary>
public sealed class GetClientPluginSlotHandler
{
    /// <summary>Checks the slot, the caller and the context.</summary>
    private readonly ClientPluginSlotGate _gate;

    /// <summary>Lists the widgets of the slot.</summary>
    private readonly PluginSlotIndexBuilder _index;

    /// <summary>Initializes the handler.</summary>
    /// <param name="gate">Checks the slot, the caller and the context.</param>
    /// <param name="index">Lists the widgets of the slot.</param>
    public GetClientPluginSlotHandler(ClientPluginSlotGate gate, PluginSlotIndexBuilder index)
    {
        _gate = gate;
        _index = index;
    }

    /// <summary>Lists the widgets of the slot for the caller and the context.</summary>
    /// <param name="query">The slot and its context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The index; <c>PLUGIN_SLOT_NOT_FOUND</c> when the host offers no such client slot; <c>IDENTITY_PLUGIN_UNAVAILABLE</c>
    /// when the identity plugin could not say who the caller is; <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller acts for no
    /// account; <c>PLUGIN_SLOT_CONTEXT_REQUIRED</c> when the slot needs a service and none is named; and
    /// <c>SERVICE_NOT_FOUND</c> when the service is not the caller's or does not exist, the same answer for both.
    /// </returns>
    public async Task<Result<PluginSlotIndexDto>> HandleAsync(GetClientPluginSlotQuery query, CancellationToken ct)
    {
        var slot = ClientPluginSlotGate.FindSlot(query.SlotId);
        if (slot.IsFailure)
        {
            return Result<PluginSlotIndexDto>.FailureCode(slot.ErrorCode!);
        }

        var caller = await _gate.ResolveCallerAsync(ct);
        if (caller.IsFailure)
        {
            return Result<PluginSlotIndexDto>.FailureCode(caller.ErrorCode!);
        }

        var product = await _gate.VerifyContextAsync(slot.Value!, query.ServiceId, ct);
        if (product.IsFailure)
        {
            return Result<PluginSlotIndexDto>.FailureCode(product.ErrorCode!);
        }

        return Result<PluginSlotIndexDto>.Success(
            await _index.BuildAsync(slot.Value!, caller.Value!.Held, product.Value, ct));
    }
}
