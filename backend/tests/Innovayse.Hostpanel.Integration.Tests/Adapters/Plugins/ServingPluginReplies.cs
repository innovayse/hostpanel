using System.Globalization;
using System.Net;
using Hostpanel.Sdk.Plugins.Endpoints;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Host;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// How every test plugin that implements <see cref="IPluginEndpoints"/> answers, so the fulfilment, payment, registrar and
/// provisioning fixtures share one implementation: an empty answer for each declared route, and for the connection route
/// (<see cref="PluginCapabilitySeeds.ConnectionPath"/>) the server connection of the service named in the path, asked of the
/// host the way a real plugin asks it (#1357).
/// </summary>
internal static class ServingPluginReplies
{
    /// <summary>The route value that carries the service id on the connection route.</summary>
    private const string ServiceIdValue = "serviceId";

    /// <summary>Answers one call.</summary>
    /// <param name="call">The call the host hands the plugin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The server's host name and the provisioning reference on the connection route, a problem when the host refuses, else no content.</returns>
    public static async Task<PluginReply> AnswerAsync(PluginCall call, CancellationToken ct)
    {
        if (call.Route != PluginCapabilitySeeds.ConnectionPath)
        {
            return PluginReply.NoContent();
        }

        var serviceId = int.Parse(call.RouteValues[ServiceIdValue], CultureInfo.InvariantCulture);
        try
        {
            var context = await ((IPluginHostServiceConnectionApi)call.Host).ResolveServiceConnectionAsync(serviceId, ct);
            return PluginReply.Json(new { hostname = context.Server.Hostname, provisioningRef = context.Service.ProvisioningRef });
        }
        catch (PluginHostAccessException refused)
        {
            return PluginReply.Problem(StatusOf(refused.Failure), refused.Failure.ToString().ToLowerInvariant());
        }
    }

    /// <summary>The status a plugin that does not handle a refusal itself answers with.</summary>
    /// <param name="failure">Why the host refused.</param>
    /// <returns>403 for a refusal of the plugin itself, 404 for a service that is not found, else 409.</returns>
    private static int StatusOf(PluginHostAccessFailure failure)
    {
        return failure switch
        {
            PluginHostAccessFailure.NotAllowed => (int)HttpStatusCode.Forbidden,
            PluginHostAccessFailure.NotFound => (int)HttpStatusCode.NotFound,
            _ => (int)HttpStatusCode.Conflict,
        };
    }
}
