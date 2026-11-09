using System.Net;
using System.Net.Sockets;
using Innovayse.Hostpanel.Infrastructure.Security.Enums;
using Innovayse.Hostpanel.Infrastructure.Security.Settings;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Security;

/// <summary>
/// Resolves a host this server was told to dial (a provisioning server, a registrar API, an SSH key
/// agent) and connects only to the addresses <see cref="OutboundAddressPolicy"/> allows.
/// </summary>
/// <remarks>
/// <para>
/// Used as the <see cref="SocketsHttpHandler.ConnectCallback"/> of every guarded HTTP client and,
/// through <see cref="ResolveAsync"/>, by the SSH agent transport. Checking the addresses actually
/// dialled, at connect time, is what closes the gap a check on the typed host alone leaves open: a
/// name that resolves, or later re-resolves, to an internal address.
/// </para>
/// <para>
/// Loopback, link-local and metadata addresses are always refused; private ranges follow
/// <see cref="OutboundNetworkOptions.AllowPrivateNetworks"/>. Resolution and connect share one
/// deadline, <see cref="OutboundNetworkOptions.ConnectTimeout"/>.
/// </para>
/// <para>
/// A host listed in <see cref="PanelNetworkOptions"/> is dialled at its configured address instead
/// of what its name resolves to, so a panel whose API port is reachable only over a VPN keeps its
/// public name in the request, the Host header and TLS validation. That address comes from the
/// deployment's own configuration, not from an admin-entered value, so it is dialled as configured.
/// </para>
/// </remarks>
public sealed class OutboundConnector
{
    /// <summary>Which ranges are allowed and how long a connect may take.</summary>
    private readonly OutboundNetworkOptions _options;

    /// <summary>Hosts the deployment routes to a fixed address, keyed case-insensitively by host name.</summary>
    private readonly IReadOnlyDictionary<string, IPAddress> _routes;

    /// <summary>Initializes the connector.</summary>
    /// <param name="options">Which ranges are allowed and how long a connect may take.</param>
    /// <param name="routes">Hosts the deployment routes to a fixed address, such as a panel over a VPN.</param>
    public OutboundConnector(IOptions<OutboundNetworkOptions> options, IOptions<PanelNetworkOptions> routes)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(routes);

        _options = options.Value;
        _routes = routes.Value.Map;
    }

    /// <summary>Gets the deadline for resolution plus connect.</summary>
    public TimeSpan ConnectTimeout => _options.ConnectTimeout;

    /// <summary>
    /// Resolves a host and keeps only the addresses that may be dialled; a host the deployment routes
    /// to a fixed address answers with that address alone.
    /// </summary>
    /// <param name="host">The host name or address literal to reach.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The allowed addresses, at least one.</returns>
    /// <exception cref="OutboundAddressRefusedException">No address of the host may be dialled.</exception>
    public async Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        // An operator's configured route is trusted configuration, not admin input, so the address
        // policy that guards typed hosts does not apply to it.
        if (_routes.TryGetValue(host, out var routed))
        {
            return [routed];
        }

        var resolved = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, ct);
        var allowed = resolved.Where(IsAllowed).ToArray();
        if (allowed.Length == 0)
        {
            throw new OutboundAddressRefusedException(host);
        }

        return allowed;
    }

    /// <summary>Opens the connection to an allowed address of the requested host.</summary>
    /// <param name="context">The host and port the handler wants to reach.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connected stream.</returns>
    /// <exception cref="OutboundAddressRefusedException">No address of the host may be dialled.</exception>
    public async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(_options.ConnectTimeout);

        var endpoint = context.DnsEndPoint;
        var allowed = await ResolveAsync(endpoint.Host, deadline.Token);
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(allowed, endpoint.Port, deadline.Token);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch (Exception)
        {
            // Not handled here: the socket is released and the error, cancellation included, goes on.
            socket.Dispose();
            throw;
        }
    }

    /// <summary>Tells whether one resolved address may be dialled under the current options.</summary>
    /// <param name="address">One address the host name resolved to.</param>
    /// <returns><c>true</c> for a public address, or a private one while private networks are allowed.</returns>
    private bool IsAllowed(IPAddress address)
    {
        return OutboundAddressPolicy.Classify(address) switch
        {
            OutboundAddressClass.Public => true,
            OutboundAddressClass.Private => _options.AllowPrivateNetworks,
            _ => false,
        };
    }
}
