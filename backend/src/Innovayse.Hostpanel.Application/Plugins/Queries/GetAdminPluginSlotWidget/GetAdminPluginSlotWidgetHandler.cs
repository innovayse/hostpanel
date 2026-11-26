using Hostpanel.Sdk.Plugins.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetAdminPluginSlotWidget;

/// <summary>
/// Handles <see cref="GetAdminPluginSlotWidgetQuery"/>. The administrator may see every record, so the answers keep the
/// codes of the admin slot index (an unknown slot, a context that is required or names nothing) and only a widget that
/// is not listed for that context, or whose view is missing, is <c>PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND</c>. A widget is
/// served only when the slot index would list it (same gate, same <see cref="PluginSlotIndexBuilder"/>), and its
/// document comes from <see cref="PluginSlotWidgetViewReader"/>, shared with the client panel, so no plugin is built
/// or called.
/// </summary>
public sealed class GetAdminPluginSlotWidgetHandler
{
    /// <summary>Checks the slot and the context the page shows.</summary>
    private readonly AdminPluginSlotGate _gate;

    /// <summary>Lists the widgets of the slot, the one rule for whether a widget shows.</summary>
    private readonly PluginSlotIndexBuilder _index;

    /// <summary>Reads the view of the widget the index lists, the part shared with the client widget view.</summary>
    private readonly PluginSlotWidgetViewReader _views;

    /// <summary>Initializes the handler.</summary>
    /// <param name="gate">Checks the slot and the context the page shows.</param>
    /// <param name="index">Lists the widgets of the slot.</param>
    /// <param name="views">Reads the view of the widget the index lists.</param>
    public GetAdminPluginSlotWidgetHandler(AdminPluginSlotGate gate, PluginSlotIndexBuilder index, PluginSlotWidgetViewReader views)
    {
        _gate = gate;
        _index = index;
        _views = views;
    }

    /// <summary>Reads the widget's view for the context.</summary>
    /// <param name="query">The slot, the widget and the context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The widget with its view; <c>PLUGIN_SLOT_NOT_FOUND</c> when the host offers no such admin slot;
    /// <c>PLUGIN_SLOT_CONTEXT_REQUIRED</c> when the slot needs a service or client and none is named;
    /// <c>SERVICE_NOT_FOUND</c> when the service does not exist or the client named with it is not its own;
    /// <c>CLIENT_NOT_FOUND</c> when the client does not exist; <c>PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND</c> when the widget is
    /// unknown, hidden, bound elsewhere, of a plugin that is off, or has no view.
    /// </returns>
    public async Task<Result<PluginSlotWidgetViewDto>> HandleAsync(GetAdminPluginSlotWidgetQuery query, CancellationToken ct)
    {
        var slot = AdminPluginSlotGate.FindSlot(query.SlotId);
        if (slot.IsFailure)
        {
            return Result<PluginSlotWidgetViewDto>.FailureCode(slot.ErrorCode!);
        }

        var product = await _gate.VerifyContextAsync(slot.Value!, query.ServiceId, query.ClientId, ct);
        if (product.IsFailure)
        {
            return Result<PluginSlotWidgetViewDto>.FailureCode(product.ErrorCode!);
        }

        var index = await _index.BuildAsync(slot.Value!, ClientPermission.None, product.Value, ct);
        var view = await _views.ReadAsync(index, query.PluginId, query.WidgetId, PluginArea.Admin, ClientPermission.None, ct);

        return view is null
            ? Result<PluginSlotWidgetViewDto>.FailureCode(HostpanelErrorCodes.PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND)
            : Result<PluginSlotWidgetViewDto>.Success(view);
    }
}
