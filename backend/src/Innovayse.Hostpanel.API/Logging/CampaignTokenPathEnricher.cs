using Innovayse.Hostpanel.Application.Campaigns.Common;
using Serilog.Core;
using Serilog.Events;

namespace Innovayse.Hostpanel.API.Logging;

/// <summary>
/// Replaces the token in the path of a campaign mail's open and click addresses with <see cref="Mask"/> before a request log
/// line is written. The token is a credential for one recipient's mail, so a log line keeps the route and drops it.
/// </summary>
/// <remarks>
/// Serilog's request logging records the path without its query string, so the unsubscribe token, which travels in the query
/// of the one-click address, never reaches the log from here; the edge's access log is masked by the nginx template.
/// </remarks>
public sealed class CampaignTokenPathEnricher : ILogEventEnricher
{
    /// <summary>What stands in the log where the token was.</summary>
    public const string Mask = "***";

    /// <summary>The property request logging puts the path in.</summary>
    private const string PathProperty = "RequestPath";

    /// <summary>The path every open and click address starts with.</summary>
    private const string Prefix = "/" + CampaignTrackingLinks.RoutePrefix + "/";

    /// <summary>Masks the token in a path.</summary>
    /// <param name="path">The request path.</param>
    /// <returns>The path with the token of a tracking address replaced by <see cref="Mask"/>; any other path unchanged.</returns>
    public static string MaskPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!path.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        var segments = path[Prefix.Length..].Split('/');
        var tracked = segments[0].Equals(CampaignTrackingLinks.OpenSegment, StringComparison.OrdinalIgnoreCase)
            || segments[0].Equals(CampaignTrackingLinks.ClickSegment, StringComparison.OrdinalIgnoreCase);
        if (!tracked || segments.Length < 2)
        {
            return path;
        }

        segments[1] = Mask;
        return path[..Prefix.Length] + string.Join('/', segments);
    }

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        if (logEvent.Properties.TryGetValue(PathProperty, out var value) && value is ScalarValue { Value: string path })
        {
            var masked = MaskPath(path);
            if (!string.Equals(masked, path, StringComparison.Ordinal))
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(PathProperty, masked));
            }
        }
    }
}
