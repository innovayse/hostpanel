using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Host;
using Innovayse.Hostpanel.Application.Services.Common;

namespace Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;

/// <summary>Turns what a host access answered into what the SDK promises a plugin: the value, or a thrown refusal.</summary>
public static class PluginHostOutcomeExtensions
{
    /// <summary>Returns the granted value, or throws the refusal as the SDK's exception at the plugin boundary.</summary>
    /// <typeparam name="T">What the access hands the plugin.</typeparam>
    /// <param name="outcome">What the host access answered.</param>
    /// <returns>The granted value.</returns>
    /// <exception cref="PluginHostAccessException">The host refused; carries the reason, never the value.</exception>
    public static T GrantedOrThrow<T>(this PluginHostOutcome<T> outcome)
        where T : class
    {
        return outcome.Value ?? throw new PluginHostAccessException(outcome.Failure ?? PluginHostAccessFailure.ServerUnavailable);
    }

    /// <summary>Throws the refusal as the SDK's exception when there is one.</summary>
    /// <param name="failure">The refusal a removal answered, or <see langword="null"/> when it succeeded.</param>
    /// <exception cref="PluginHostAccessException">The host refused.</exception>
    public static void ThrowIfRefused(this PluginHostAccessFailure? failure)
    {
        if (failure is { } refused)
        {
            throw new PluginHostAccessException(refused);
        }
    }
}
