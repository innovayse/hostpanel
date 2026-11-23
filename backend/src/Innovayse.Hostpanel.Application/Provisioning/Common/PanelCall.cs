using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Provisioning.Common;

/// <summary>
/// Sends one call to a hosting panel and turns its failure into a <see cref="Result"/>: the one place that knows a panel plugin's
/// coded refusal (<see cref="PanelErrorCodes"/>) reaches the caller as its own code while every other failure is logged and
/// answered with the caller's generic upstream failure code, so no provider text ever leaves the host.
/// </summary>
public static class PanelCall
{
    /// <summary>Runs the call and maps what it throws.</summary>
    /// <typeparam name="T">What the call returns.</typeparam>
    /// <param name="call">The provider call.</param>
    /// <param name="logger">Records a refusal or a failure.</param>
    /// <param name="action">What is being done, as a log phrase such as <c>Changing password</c>.</param>
    /// <param name="serviceId">The service the call is for.</param>
    /// <param name="serverId">The server the call goes to.</param>
    /// <param name="upstreamFailedCode">The code answered for a failure that is not a coded refusal.</param>
    /// <param name="ownCodes">Codes the provider may refuse with besides the panel family, which the caller also passes through.</param>
    /// <returns>Success with the call's value, or the refusal's own code, or <paramref name="upstreamFailedCode"/>.</returns>
    public static async Task<Result<T>> RunAsync<T>(
        Func<Task<T>> call,
        ILogger logger,
        string action,
        int serviceId,
        int serverId,
        string upstreamFailedCode,
        params string[] ownCodes)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(logger);
        try
        {
            return Result<T>.Success(await call());
        }
        catch (DomainRuleException ex) when (PanelErrorCodes.Contains(ex.Code) || ownCodes.Contains(ex.Code))
        {
            // A refusal with a reason the caller can act on: the caller gets that sentence, not the generic failure.
            logger.LogWarning("{Action} for service {ServiceId} on server {ServerId} was refused: {ErrorCode}.", action, serviceId, serverId, ex.Code);
            return Result<T>.FailureCode(ex.Code);
        }
        catch (Exception ex)
        {
            // The provider's own message and the server it named never reach the caller (rules on upstream detail); they are
            // logged here for whoever has to chase the failure up.
            logger.LogWarning(ex, "{Action} for service {ServiceId} on server {ServerId} failed.", action, serviceId, serverId);
            return Result<T>.FailureCode(upstreamFailedCode);
        }
    }

    /// <summary>Runs a call that returns nothing and maps what it throws.</summary>
    /// <param name="call">The provider call.</param>
    /// <param name="logger">Records a refusal or a failure.</param>
    /// <param name="action">What is being done, as a log phrase such as <c>Changing password</c>.</param>
    /// <param name="serviceId">The service the call is for.</param>
    /// <param name="serverId">The server the call goes to.</param>
    /// <param name="upstreamFailedCode">The code answered for a failure that is not a coded refusal.</param>
    /// <returns>Success, or the refusal's own code, or <paramref name="upstreamFailedCode"/>.</returns>
    public static async Task<Result> RunAsync(
        Func<Task> call,
        ILogger logger,
        string action,
        int serviceId,
        int serverId,
        string upstreamFailedCode)
    {
        ArgumentNullException.ThrowIfNull(call);
        var outcome = await RunAsync(
            async () =>
            {
                await call();
                return true;
            },
            logger,
            action,
            serviceId,
            serverId,
            upstreamFailedCode);
        return outcome.IsSuccess ? Result.Success() : Result.FailureCode(outcome.ErrorCode!);
    }
}
