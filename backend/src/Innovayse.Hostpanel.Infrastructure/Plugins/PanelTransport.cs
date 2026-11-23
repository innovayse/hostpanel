using System.Net.Security;
using Hostpanel.Sdk.Plugins.Provisioning;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Resilience;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The host's <see cref="IPanelTransport"/>: the only way a panel, virtual server or registrar plugin reaches the other
/// side over the network. Every client it hands out dials through <see cref="OutboundConnector"/> (loopback, link-local and
/// metadata addresses are refused, private ranges follow the deployment's setting and the panel route map applies), follows
/// no redirect, has no retry stage and is cut off by <see cref="HttpBackstopTimeouts.PanelTransport"/>, so a plugin can
/// neither go around the host's rules nor have a request repeated behind its back.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CreateClient(PanelServerConnection)"/> honours the operator's per server setting "accept a self signed
/// certificate" (<see cref="Server.AcceptsSelfSignedCertificate"/>). The setting is read from the stored
/// server, never from the connection the plugin passes, and the client it allows trusts a certificate only for the host the stored
/// server is dialled at (its API address, or its host name when none is set): the check is made on the name of the connection being secured, so the same client reaching any other host
/// still refuses a certificate that does not validate. The host keeps no panel port, so the allowance is the host name
/// and not a port of it. The certificate is never trusted for another server, for a client from
/// <see cref="CreateClient()"/> or for the process.
/// </para>
/// <para>
/// A client for a trusted server owns its own handler, because trust is per server and the factory shares one handler per
/// name; the handler is the guarded one (<see cref="OutboundConnector.CreateHandler"/>), so it refuses the same addresses,
/// and its idle connections close after <see cref="IdleConnectionTimeout"/> so a client a plugin lets go is not kept alive.
/// </para>
/// </remarks>
public sealed class PanelTransport : IPanelTransport
{
    /// <summary>How long an idle connection of a per server client stays open, so one the plugin let go does not outlive its use.</summary>
    private static readonly TimeSpan IdleConnectionTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The longest a connection of a per server client is reused, so a changed address or certificate is met again.</summary>
    private static readonly TimeSpan ConnectionLifetime = TimeSpan.FromMinutes(2);

    /// <summary>What a self signed certificate is wrong with: its chain does not end in a trusted root and its name is not the host's.</summary>
    private const SslPolicyErrors SelfSignedFaults =
        SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch;

    /// <summary>Hands out the guarded named client.</summary>
    private readonly IHttpClientFactory _factory;

    /// <summary>Builds the guarded handler of a client that trusts a self signed certificate.</summary>
    private readonly OutboundConnector _connector;

    /// <summary>Opens the scope, and with it the data context of its own, that reads the operator's setting of a server.</summary>
    private readonly IServiceScopeFactory _scopes;

    /// <summary>Initializes a new instance of the <see cref="PanelTransport"/> class.</summary>
    /// <param name="factory">Hands out the guarded named client.</param>
    /// <param name="connector">Builds the guarded handler of a client that trusts a self signed certificate.</param>
    /// <param name="scopes">Opens the scope, and with it the data context of its own, that reads the operator's setting of a server.</param>
    public PanelTransport(IHttpClientFactory factory, OutboundConnector connector, IServiceScopeFactory scopes)
    {
        _factory = factory;
        _connector = connector;
        _scopes = scopes;
    }

    /// <inheritdoc/>
    public HttpClient CreateClient()
    {
        return _factory.CreateClient(HttpClientNames.PanelTransport);
    }

    /// <inheritdoc/>
    public HttpClient CreateClient(PanelServerConnection server)
    {
        ArgumentNullException.ThrowIfNull(server);

        var trustedHost = ReadTrustedHost(server.ServerId);
        if (trustedHost is null)
        {
            return CreateClient();
        }

        var handler = _connector.CreateHandler(followRedirects: false);
        handler.PooledConnectionIdleTimeout = IdleConnectionTimeout;
        handler.PooledConnectionLifetime = ConnectionLifetime;
        handler.SslOptions.RemoteCertificateValidationCallback =
            (sender, _, _, errors) => IsAccepted(sender, errors, trustedHost);
        return new HttpClient(handler, disposeHandler: true) { Timeout = HttpBackstopTimeouts.PanelTransport };
    }

    /// <summary>
    /// Reads the host the operator trusts for a server (the one its API is dialled at), or nothing when the server does not accept a self signed
    /// certificate or does not exist. It is synchronous because the SDK member is: the answer is one indexed row. It runs on a
    /// data context of its own, so it can never collide with an operation running on the caller's context.
    /// </summary>
    /// <param name="serverId">The id of the stored server.</param>
    /// <returns>The stored API host, or <see langword="null"/> when no certificate is trusted for the server.</returns>
    private string? ReadTrustedHost(int serverId)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Only the two address columns leave the database: the row also holds the encrypted credentials, which this read never needs.
        var addresses = db.Servers.AsNoTracking()
            .Where(s => s.Id == serverId && s.AcceptsSelfSignedCertificate)
            .Select(s => new { s.Hostname, s.ApiHost })
            .FirstOrDefault();
        return addresses is null ? null : Server.ResolveApiEndpointHost(addresses.Hostname, addresses.ApiHost);
    }

    /// <summary>
    /// Decides whether the certificate of a connection is accepted: when it validates, always; when it does not, only for
    /// the trusted host name, and only for the faults a self signed certificate has (an untrusted chain and a name that does
    /// not match). A certificate that was not presented is never accepted.
    /// </summary>
    /// <param name="sender">The stream being secured, which names the host it was opened for.</param>
    /// <param name="errors">What the platform found wrong with the certificate.</param>
    /// <param name="trustedHost">The host the operator trusts, as the stored server is dialled at.</param>
    /// <returns><see langword="true"/> when the connection may go on.</returns>
    private static bool IsAccepted(object sender, SslPolicyErrors errors, string trustedHost)
    {
        if (errors == SslPolicyErrors.None)
        {
            return true;
        }

        return (errors & ~SelfSignedFaults) == SslPolicyErrors.None
            && sender is SslStream stream
            && string.Equals(stream.TargetHostName, trustedHost.Trim('[', ']'), StringComparison.OrdinalIgnoreCase);
    }
}
