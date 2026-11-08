using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Microsoft.Extensions.Options;

/*
 * Why the list is computed per request rather than taken from a view: a view is written by the plugin, and the whole
 * point of the preview's rule is that a plugin cannot frame what the host did not allow. The client's domains come from
 * the account the token acts for, so no request value can name another client's, and they follow the same visibility
 * rule as the host API: a sub-user who may not see services does not get their addresses here either. And a plugin
 * reaches a client's sites only on products the operator bound to it (#823), the rule SSH access follows: a manifest's own
 * productIds are the author's claim and never count, and nothing bound means no domains. A client types a service's
 * domain at order, so it could name the panel's own host: the browser check refuses that too, and leaving it out here keeps
 * it off the frame policy as well.
 */

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>Works out the origins a <c>preview</c> block may frame for the current caller on one plugin's page.</summary>
public sealed class PluginFrameOriginResolver
{
    /// <summary>
    /// The longest space-joined list, in characters. The list becomes one response header, and a proxy answers 502 for
    /// headers above its buffer (commonly 4 KiB in all), so an origin past this is left out: its preview shows nothing.
    /// </summary>
    public const int MaxJoinedLength = 2048;

    /// <summary>The account the caller acts for.</summary>
    private readonly ICallerClientResolver _callerClient;

    /// <summary>Reads the domains of that account's services.</summary>
    private readonly IPluginHostFactsReader _facts;

    /// <summary>Reads the products the operator bound the plugin's slot widgets to (#823).</summary>
    private readonly IPluginSlotContextReader _slots;

    /// <summary>The origins allowed over plain http on a developer machine.</summary>
    private readonly IOptions<PluginPreviewOptions> _options;

    /// <summary>The panel's own address, whose origin is never framed.</summary>
    private readonly IOptions<ClientPortalOptions> _portal;

    /// <summary>Initializes the resolver.</summary>
    /// <param name="callerClient">The account the caller acts for.</param>
    /// <param name="facts">Reads the domains of that account's services.</param>
    /// <param name="slots">Reads the products the operator bound the plugin's slot widgets to (#823).</param>
    /// <param name="options">The origins allowed over plain http on a developer machine.</param>
    /// <param name="portal">The panel's own address, whose origin is never framed.</param>
    public PluginFrameOriginResolver(
        ICallerClientResolver callerClient,
        IPluginHostFactsReader facts,
        IPluginSlotContextReader slots,
        IOptions<PluginPreviewOptions> options,
        IOptions<ClientPortalOptions> portal)
    {
        _callerClient = callerClient;
        _facts = facts;
        _slots = slots;
        _options = options;
        _portal = portal;
    }

    /// <summary>Lists the origins, first what the plugin declares, then the caller's own, then the development ones.</summary>
    /// <param name="manifest">The plugin's manifest.</param>
    /// <param name="held">The permissions the caller holds on the account.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The distinct canonical origins within <see cref="MaxJoinedLength"/>; empty when none is allowed.</returns>
    public async Task<IReadOnlyList<string>> ResolveAsync(PluginManifest manifest, ClientPermission held, CancellationToken ct)
    {
        var origins = new List<string>();
        foreach (var entry in manifest.ExternalOrigins)
        {
            if (PluginFrameOrigin.FromHttps(entry) is { } origin)
            {
                origins.Add(origin);
            }
        }

        var access = await _callerClient.ResolveAsync(ct);
        if (access.ClientId is { } clientId && held.HasFlag(ClientPermission.ViewProductsServices))
        {
            var bindings = await _slots.ListBindingsAsync(manifest.Id, null, ct);
            var bound = bindings.Where(b => b.ProductExists).Select(b => b.ProductId).Distinct().ToList();
            if (bound.Count > 0)
            {
                var domains = await _facts.ListActiveServiceDomainsAsync(clientId, bound, ct);
                origins.AddRange(domains.SelectMany(PluginFrameOrigin.FromDomain));
            }
        }

        foreach (var entry in _options.Value.DevelopmentHttpOrigins)
        {
            if (PluginFrameOrigin.FromLoopbackHttp(entry) is { } origin)
            {
                origins.Add(origin);
            }
        }

        var own = OwnOrigin();
        return Bound(origins.Where(origin => !string.Equals(origin, own, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Works out the origins the absolute links of a view may point at: what the manifest declares, as written (wildcards
    /// included), then the <c>https</c> origins of <paramref name="frameOrigins"/>. The latter are the domains of the
    /// reader's active services on products bound to the plugin, under the same rule a preview follows, so a plugin
    /// published once for every installation can link to the reader's own sites without naming their domains. Plain-http development origins are left out.
    /// </summary>
    /// <param name="manifest">The plugin's manifest.</param>
    /// <param name="frameOrigins">The result of <see cref="ResolveAsync"/> for the same caller; empty in the admin panel.</param>
    /// <returns>The distinct origins.</returns>
    public static IReadOnlyList<string> LinkOrigins(PluginManifest manifest, IReadOnlyList<string> frameOrigins)
    {
        return manifest.ExternalOrigins
            .Concat(frameOrigins.Where(origin => origin.StartsWith("https://", StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The panel's own origin in the list's canonical form.</summary>
    /// <returns>The origin, or null for an address that is not absolute, which then matches nothing.</returns>
    private string? OwnOrigin()
    {
        return Uri.TryCreate(_portal.Value.BaseUrl, UriKind.Absolute, out var address)
            ? address.GetLeftPart(UriPartial.Authority).ToLowerInvariant()
            : null;
    }

    /// <summary>Removes repeats and cuts the list at the header bound.</summary>
    /// <param name="origins">The origins in priority order.</param>
    /// <returns>The kept origins.</returns>
    private static List<string> Bound(IEnumerable<string> origins)
    {
        var kept = new List<string>();
        var length = 0;
        foreach (var origin in origins.Distinct(StringComparer.Ordinal))
        {
            // One separator per origin keeps the count an upper bound of the joined text.
            if (length + origin.Length + 1 > MaxJoinedLength)
            {
                break;
            }

            kept.Add(origin);
            length += origin.Length + 1;
        }

        return kept;
    }
}
