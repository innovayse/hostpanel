using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Slots;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// The three checks every client slot read makes: the slot, the caller's account and the context (a service must be the
/// caller's own). Each is a step of its own so a use case can order them: the slot index checks the slot first and keeps
/// a code per refusal (its contract since #644), while the widget view resolves the caller first and folds every refusal
/// into one answer. Written once, so neither can verify a context more loosely than the other.
/// </summary>
public sealed class ClientPluginSlotGate
{
    /// <summary>Resolves the account the caller acts for and the permissions held there.</summary>
    private readonly ICallerClientResolver _callerClient;

    /// <summary>The rule that says a client may only name their own services.</summary>
    private readonly IServiceOwnership _ownership;

    /// <summary>Initializes the gate.</summary>
    /// <param name="callerClient">Resolves the account the caller acts for and the permissions held there.</param>
    /// <param name="ownership">The rule that says a client may only name their own services.</param>
    public ClientPluginSlotGate(ICallerClientResolver callerClient, IServiceOwnership ownership)
    {
        _callerClient = callerClient;
        _ownership = ownership;
    }

    /// <summary>Finds a client area slot.</summary>
    /// <param name="slotId">The slot id from the URL.</param>
    /// <returns>The slot, or <c>PLUGIN_SLOT_NOT_FOUND</c> for an unknown slot or one of the admin panel.</returns>
    public static Result<PluginSlot> FindSlot(string slotId)
    {
        var slot = PluginSlots.Find(slotId);
        if (slot is not { Area: PluginArea.Client })
        {
            return Result<PluginSlot>.FailureCode(HostpanelErrorCodes.PLUGIN_SLOT_NOT_FOUND);
        }

        return Result<PluginSlot>.Success(slot);
    }

    /// <summary>Resolves the account the caller acts for.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The caller; <c>IDENTITY_PLUGIN_UNAVAILABLE</c> or <c>CLIENT_PROFILE_NOT_FOUND</c>.</returns>
    public async Task<Result<ClientPluginSlotCaller>> ResolveCallerAsync(CancellationToken ct)
    {
        var access = await _callerClient.ResolveAsync(ct);

        // The plugin that decides the account did not answer: a retryable 503, not a refusal the caller could take for permanent.
        if (access.IdentityUnavailable)
        {
            return Result<ClientPluginSlotCaller>.FailureCode(HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE);
        }

        if (access.ClientId is not { } clientId)
        {
            return Result<ClientPluginSlotCaller>.FailureCode(HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND);
        }

        return Result<ClientPluginSlotCaller>.Success(new ClientPluginSlotCaller(clientId, access.Permissions));
    }

    /// <summary>Verifies the slot's context for a resolved caller.</summary>
    /// <param name="slot">The client slot.</param>
    /// <param name="serviceId">The service the page shows, for a slot that offers <c>serviceId</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The product of the slot's service, or <see langword="null"/> for a slot with no service;
    /// <c>PLUGIN_SLOT_CONTEXT_REQUIRED</c> or <c>SERVICE_NOT_FOUND</c> (foreign and missing alike).
    /// </returns>
    public async Task<Result<int?>> VerifyContextAsync(PluginSlot slot, int? serviceId, CancellationToken ct)
    {
        if (!slot.Offers(PluginSlotContextKeys.ServiceId))
        {
            return Result<int?>.Success(null);
        }

        if (serviceId is not { } id)
        {
            return Result<int?>.FailureCode(HostpanelErrorCodes.PLUGIN_SLOT_CONTEXT_REQUIRED);
        }

        // One query settles ownership and reads the product; a foreign and a missing service answer alike.
        var owned = await _ownership.GetOwnedProductIdAsync(id, ct);
        if (owned.IsFailure)
        {
            return Result<int?>.FailureCode(owned.ErrorCode!);
        }

        return Result<int?>.Success(owned.Value);
    }
}
