using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetClientPluginSlotWidget;

/// <summary>
/// Handles <see cref="GetClientPluginSlotWidgetQuery"/>, the way #643's page endpoint answers: the caller is resolved
/// first, and every refusal after that (an unknown or admin slot, a caller who acts for no account, a missing or foreign
/// service, a widget that is unknown, hidden, not permitted, bound to another product or of a plugin that is off, and a
/// view document that is missing) is the same <c>PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND</c>, so the endpoint tells nobody which
/// slots, services, plugins or widgets exist. The one other answer is the identity plugin's outage. A widget is served
/// only when the slot index would list it (same gate, same <see cref="PluginSlotIndexBuilder"/>), and its document comes
/// from the views read at load, so no plugin is built or called.
/// </summary>
public sealed class GetClientPluginSlotWidgetHandler
{
    /// <summary>Checks the slot, the caller and the context.</summary>
    private readonly ClientPluginSlotGate _gate;

    /// <summary>Lists the widgets the caller may see, the one rule for whether a widget shows.</summary>
    private readonly PluginSlotIndexBuilder _index;

    /// <summary>The available plugins with their view documents.</summary>
    private readonly IPluginUiSource _plugins;

    /// <summary>Works out the origins the widget's absolute links may point at for the caller.</summary>
    private readonly PluginFrameOriginResolver _origins;

    /// <summary>Initializes the handler.</summary>
    /// <param name="gate">Checks the slot, the caller and the context.</param>
    /// <param name="index">Lists the widgets the caller may see.</param>
    /// <param name="plugins">The available plugins with their view documents.</param>
    /// <param name="origins">Works out the origins the widget's absolute links may point at for the caller.</param>
    public GetClientPluginSlotWidgetHandler(
        ClientPluginSlotGate gate, PluginSlotIndexBuilder index, IPluginUiSource plugins, PluginFrameOriginResolver origins)
    {
        _gate = gate;
        _index = index;
        _plugins = plugins;
        _origins = origins;
    }

    /// <summary>Reads the widget's view for the caller and the context.</summary>
    /// <param name="query">The slot, the widget and the context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The widget with its view; <c>IDENTITY_PLUGIN_UNAVAILABLE</c> when the identity plugin could not say who the caller
    /// is; <c>PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND</c> for every other refusal.
    /// </returns>
    public async Task<Result<PluginSlotWidgetViewDto>> HandleAsync(GetClientPluginSlotWidgetQuery query, CancellationToken ct)
    {
        // The caller first: an outage is the only answer that may differ, and it must not depend on which ids were sent.
        var caller = await _gate.ResolveCallerAsync(ct);
        if (caller.ErrorCode == HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE)
        {
            return Result<PluginSlotWidgetViewDto>.FailureCode(HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE);
        }

        var slot = ClientPluginSlotGate.FindSlot(query.SlotId);
        if (caller.IsFailure || slot.IsFailure)
        {
            return NotFound();
        }

        if (!TryReadServiceId(query.ServiceId, out var serviceId))
        {
            return NotFound();
        }

        var product = await _gate.VerifyContextAsync(slot.Value!, serviceId, ct);
        if (product.IsFailure)
        {
            return NotFound();
        }

        var index = await _index.BuildAsync(slot.Value!, caller.Value!.Held, product.Value, ct);
        var widget = index.Widgets.FirstOrDefault(w =>
            string.Equals(w.PluginId, query.PluginId, StringComparison.Ordinal)
            && string.Equals(w.Id, query.WidgetId, StringComparison.Ordinal));
        if (widget is null)
        {
            return NotFound();
        }

        // The index already decided the widget shows; the package is read only for its view document and its texts.
        // It can only narrow that answer: a plugin that went unavailable between the two reads answers the same 404,
        // and nothing it holds can widen what the index listed.
        var package = await _plugins.FindAvailableAsync(widget.PluginId, ct);
        if (package?.Views.Localised(widget.View, PluginUiTexts.For(package.Manifest)) is not { } document)
        {
            return NotFound();
        }

        // Same rule as a plugin page: the manifest's origins plus the https host of the caller's own bound services.
        var frameOrigins = await _origins.ResolveAsync(package.Manifest, caller.Value!.Held, ct);

        return Result<PluginSlotWidgetViewDto>.Success(
            new PluginSlotWidgetViewDto(
                widget.PluginId, widget.Id, widget.Title, document, PluginFrameOriginResolver.LinkOrigins(package.Manifest, frameOrigins)));
    }

    /// <summary>Reads the optional service id of the query string.</summary>
    /// <param name="text">The value as sent, or <see langword="null"/> when none was.</param>
    /// <param name="serviceId">The id, or <see langword="null"/> when none was sent.</param>
    /// <returns><see langword="false"/> for a value that is not a positive whole number, which is refused like the rest.</returns>
    private static bool TryReadServiceId(string? text, out int? serviceId)
    {
        serviceId = null;
        if (text is null)
        {
            return true;
        }

        if (!int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id)
            || id <= 0)
        {
            return false;
        }

        serviceId = id;
        return true;
    }

    /// <summary>The one refusal every hidden case shares.</summary>
    /// <returns>A failure carrying <c>PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND</c>.</returns>
    private static Result<PluginSlotWidgetViewDto> NotFound()
    {
        return Result<PluginSlotWidgetViewDto>.FailureCode(HostpanelErrorCodes.PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND);
    }
}
