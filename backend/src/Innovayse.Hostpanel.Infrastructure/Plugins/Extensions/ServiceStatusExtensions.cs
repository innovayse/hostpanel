using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.SDK.Plugins.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;

/// <summary>
/// Maps the host's <see cref="ServiceStatus"/> to the SDK's <see cref="PluginServiceStatus"/>, by name. The two sets have
/// different numbers (the SDK starts at 1, the host at 0), so a cast would silently report the wrong state; every value is
/// listed and a value this mapping does not know fails loudly instead of being passed on.
/// </summary>
public static class ServiceStatusExtensions
{
    /// <summary>Maps a service status to the status a plugin sees.</summary>
    /// <param name="status">The host's service status.</param>
    /// <returns>The SDK's status of the same name.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="status"/> is not a known service status.</exception>
    public static PluginServiceStatus ToPlugin(this ServiceStatus status)
    {
        return status switch
        {
            ServiceStatus.Pending => PluginServiceStatus.Pending,
            ServiceStatus.Active => PluginServiceStatus.Active,
            ServiceStatus.Suspended => PluginServiceStatus.Suspended,
            ServiceStatus.Terminated => PluginServiceStatus.Terminated,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The service status has no plugin equivalent."),
        };
    }
}
