using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Slots;

namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetClientPluginSlot;

/// <summary>
/// Handles <see cref="GetClientPluginSlotQuery"/>. The order is the contract: the slot, then the caller's account, then
/// the context (a service must be the caller's own, <see cref="IServiceOwnership"/>), and only then the index is built, so
/// nothing is listed for a context the caller may not see and a foreign service id cannot be told from a missing one.
/// </summary>
public sealed class GetClientPluginSlotHandler
{
    /// <summary>Resolves the account the caller acts for and the permissions held there.</summary>
    private readonly ICallerClientResolver _callerClient;

    /// <summary>The rule that says a client may only name their own services.</summary>
    private readonly IServiceOwnership _ownership;

    /// <summary>Lists the widgets of the slot.</summary>
    private readonly PluginSlotIndexBuilder _index;

    /// <summary>Initializes the handler.</summary>
    /// <param name="callerClient">Resolves the account the caller acts for and the permissions held there.</param>
    /// <param name="ownership">The rule that says a client may only name their own services.</param>
    /// <param name="index">Lists the widgets of the slot.</param>
    public GetClientPluginSlotHandler(
        ICallerClientResolver callerClient, IServiceOwnership ownership, PluginSlotIndexBuilder index)
    {
        _callerClient = callerClient;
        _ownership = ownership;
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
        var slot = PluginSlots.Find(query.SlotId);
        if (slot is not { Area: PluginArea.Client })
        {
            return Result<PluginSlotIndexDto>.FailureCode(HostpanelErrorCodes.PLUGIN_SLOT_NOT_FOUND);
        }

        var access = await _callerClient.ResolveAsync(ct);

        // The plugin that decides the account did not answer: a retryable 503, not a refusal the caller could take for permanent.
        if (access.IdentityUnavailable)
        {
            return Result<PluginSlotIndexDto>.FailureCode(HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE);
        }

        if (access.ClientId is null)
        {
            return Result<PluginSlotIndexDto>.FailureCode(HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND);
        }

        int? productId = null;
        if (slot.Offers(PluginSlotContextKeys.ServiceId))
        {
            if (query.ServiceId is not { } serviceId)
            {
                return Result<PluginSlotIndexDto>.FailureCode(HostpanelErrorCodes.PLUGIN_SLOT_CONTEXT_REQUIRED);
            }

            // One query settles ownership and reads the product; a foreign and a missing service answer alike.
            var owned = await _ownership.GetOwnedProductIdAsync(serviceId, ct);
            if (owned.IsFailure)
            {
                return Result<PluginSlotIndexDto>.FailureCode(owned.ErrorCode!);
            }

            productId = owned.Value;
        }

        return Result<PluginSlotIndexDto>.Success(await _index.BuildAsync(slot, access.Permissions, productId, ct));
    }
}
