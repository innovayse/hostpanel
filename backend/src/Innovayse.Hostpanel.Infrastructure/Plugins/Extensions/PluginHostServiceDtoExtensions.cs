using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.SDK.Plugins.Host;

namespace Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;

/// <summary>Maps the host's <see cref="PluginHostServiceDto"/> to the SDK's <see cref="PluginClientService"/>.</summary>
public static class PluginHostServiceDtoExtensions
{
    /// <summary>Maps one client service row to the record a plugin sees, with the status translated by name.</summary>
    /// <param name="service">The service columns the host shares with a plugin.</param>
    /// <returns>The SDK's service record carrying the same values.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the service's status has no plugin equivalent.</exception>
    public static PluginClientService ToPlugin(this PluginHostServiceDto service)
    {
        return new PluginClientService(
            service.Id,
            service.ProductId,
            service.Status.ToPlugin(),
            service.Domain,
            service.CreatedAt,
            service.NextRenewalAt);
    }
}
