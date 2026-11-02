using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Slots;

namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetAdminPluginSlot;

/// <summary>
/// Handles <see cref="GetAdminPluginSlotQuery"/>. The administrator may see every record, so the context is checked for
/// existence only: the slot, then the service or client the page shows, and only then the index is built. An admin slot
/// carries no permission, so none is looked at.
/// </summary>
public sealed class GetAdminPluginSlotHandler
{
    /// <summary>Reads the service and client the page shows.</summary>
    private readonly IPluginSlotContextReader _context;

    /// <summary>Lists the widgets of the slot.</summary>
    private readonly PluginSlotIndexBuilder _index;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">Reads the service and client the page shows.</param>
    /// <param name="index">Lists the widgets of the slot.</param>
    public GetAdminPluginSlotHandler(IPluginSlotContextReader context, PluginSlotIndexBuilder index)
    {
        _context = context;
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
        var slot = PluginSlots.Find(query.SlotId);
        if (slot is not { Area: PluginArea.Admin })
        {
            return Result<PluginSlotIndexDto>.FailureCode(HostpanelErrorCodes.PLUGIN_SLOT_NOT_FOUND);
        }

        int? productId = null;
        if (slot.Offers(PluginSlotContextKeys.ServiceId))
        {
            if (query.ServiceId is not { } serviceId)
            {
                return Result<PluginSlotIndexDto>.FailureCode(HostpanelErrorCodes.PLUGIN_SLOT_CONTEXT_REQUIRED);
            }

            var service = await _context.FindServiceAsync(serviceId, ct);

            // A client id offered next to the service must be the service's own, or the context contradicts itself.
            if (service is null || (query.ClientId is { } clientId && clientId != service.ClientId))
            {
                return Result<PluginSlotIndexDto>.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
            }

            productId = service.ProductId;
        }
        else if (slot.Offers(PluginSlotContextKeys.ClientId))
        {
            if (query.ClientId is not { } clientId)
            {
                return Result<PluginSlotIndexDto>.FailureCode(HostpanelErrorCodes.PLUGIN_SLOT_CONTEXT_REQUIRED);
            }

            if (!await _context.ClientExistsAsync(clientId, ct))
            {
                return Result<PluginSlotIndexDto>.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
            }
        }

        return Result<PluginSlotIndexDto>.Success(await _index.BuildAsync(slot, ClientPermission.None, productId, ct));
    }
}
