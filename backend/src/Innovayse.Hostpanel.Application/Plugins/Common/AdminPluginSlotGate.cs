using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Slots;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// The two checks every admin slot read makes: the slot and the context (the service or client the page shows must
/// exist). The administrator may see every record, so the context is checked for existence only. Written once, so the
/// slot index and the widget view can never verify a context differently.
/// </summary>
public sealed class AdminPluginSlotGate
{
    /// <summary>Reads the service and client the page shows.</summary>
    private readonly IPluginSlotContextReader _context;

    /// <summary>Initializes the gate.</summary>
    /// <param name="context">Reads the service and client the page shows.</param>
    public AdminPluginSlotGate(IPluginSlotContextReader context)
    {
        _context = context;
    }

    /// <summary>Finds an admin area slot.</summary>
    /// <param name="slotId">The slot id from the URL.</param>
    /// <returns>The slot, or <c>PLUGIN_SLOT_NOT_FOUND</c> for an unknown slot or one of the client panel.</returns>
    public static Result<PluginSlot> FindSlot(string slotId)
    {
        var slot = PluginSlots.Find(slotId);
        if (slot is not { Area: PluginArea.Admin })
        {
            return Result<PluginSlot>.FailureCode(HostpanelErrorCodes.PLUGIN_SLOT_NOT_FOUND);
        }

        return Result<PluginSlot>.Success(slot);
    }

    /// <summary>Verifies the slot's context.</summary>
    /// <param name="slot">The admin slot.</param>
    /// <param name="serviceId">The service the page shows, for a slot that offers <c>serviceId</c>.</param>
    /// <param name="clientId">The client the page shows, for a slot that offers <c>clientId</c>; on a service slot it must be the service's own client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The product of the slot's service, or <see langword="null"/> for a slot with no service;
    /// <c>PLUGIN_SLOT_CONTEXT_REQUIRED</c> when the slot needs a service or client and none is named;
    /// <c>SERVICE_NOT_FOUND</c> when the service does not exist or the client named with it is not its own;
    /// <c>CLIENT_NOT_FOUND</c> when the client does not exist.
    /// </returns>
    public async Task<Result<int?>> VerifyContextAsync(PluginSlot slot, int? serviceId, int? clientId, CancellationToken ct)
    {
        if (slot.Offers(PluginSlotContextKeys.ServiceId))
        {
            if (serviceId is not { } id)
            {
                return Result<int?>.FailureCode(HostpanelErrorCodes.PLUGIN_SLOT_CONTEXT_REQUIRED);
            }

            var service = await _context.FindServiceAsync(id, ct);

            // A client id offered next to the service must be the service's own, or the context contradicts itself.
            if (service is null || (clientId is { } offered && offered != service.ClientId))
            {
                return Result<int?>.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
            }

            return Result<int?>.Success(service.ProductId);
        }

        if (slot.Offers(PluginSlotContextKeys.ClientId))
        {
            if (clientId is not { } id)
            {
                return Result<int?>.FailureCode(HostpanelErrorCodes.PLUGIN_SLOT_CONTEXT_REQUIRED);
            }

            if (!await _context.ClientExistsAsync(id, ct))
            {
                return Result<int?>.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
            }
        }

        return Result<int?>.Success(null);
    }
}
