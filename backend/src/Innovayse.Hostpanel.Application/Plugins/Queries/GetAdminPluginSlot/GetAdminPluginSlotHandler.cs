using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetAdminPluginSlot;

/// <summary>
/// Handles <see cref="GetAdminPluginSlotQuery"/>. The administrator may see every record, so the context is checked for
/// existence only: the slot, then the service or client the page shows, and only then the index is built. An admin slot
/// carries no permission, so none is looked at.
/// </summary>
public sealed class GetAdminPluginSlotHandler
{
    /// <summary>Checks the slot and the context the page shows.</summary>
    private readonly AdminPluginSlotGate _gate;

    /// <summary>Lists the widgets of the slot.</summary>
    private readonly PluginSlotIndexBuilder _index;

    /// <summary>Initializes the handler.</summary>
    /// <param name="gate">Checks the slot and the context the page shows.</param>
    /// <param name="index">Lists the widgets of the slot.</param>
    public GetAdminPluginSlotHandler(AdminPluginSlotGate gate, PluginSlotIndexBuilder index)
    {
        _gate = gate;
        _index = index;
    }

    /// <summary>Lists the widgets of the slot for the context.</summary>
    /// <param name="query">The slot and its context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The index; <c>PLUGIN_SLOT_NOT_FOUND</c> when the host offers no such admin slot; <c>PLUGIN_SLOT_CONTEXT_REQUIRED</c>
    /// when the slot needs a service or client and none is named; <c>SERVICE_NOT_FOUND</c> when the service does not exist
    /// or the client named with it is not its own; <c>CLIENT_NOT_FOUND</c> when the client does not exist.
    /// </returns>
    public async Task<Result<PluginSlotIndexDto>> HandleAsync(GetAdminPluginSlotQuery query, CancellationToken ct)
    {
        var slot = AdminPluginSlotGate.FindSlot(query.SlotId);
        if (slot.IsFailure)
        {
            return Result<PluginSlotIndexDto>.FailureCode(slot.ErrorCode!);
        }

        var product = await _gate.VerifyContextAsync(slot.Value!, query.ServiceId, query.ClientId, ct);
        if (product.IsFailure)
        {
            return Result<PluginSlotIndexDto>.FailureCode(product.ErrorCode!);
        }

        return Result<PluginSlotIndexDto>.Success(await _index.BuildAsync(slot.Value!, ClientPermission.None, product.Value, ct));
    }
}
