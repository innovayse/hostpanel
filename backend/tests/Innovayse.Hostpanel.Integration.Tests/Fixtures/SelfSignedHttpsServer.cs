using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A real Kestrel server on a random loopback port that answers every request over TLS with a self signed certificate nobody
/// trusts, the way a hosting panel on a private port does. It records the paths it was asked for, so a test can tell a request
/// that completed the handshake from one the client refused before it was sent.
/// </summary>
public sealed class SelfSignedHttpsServer : IAsyncDisposable
{
    /// <summary>The running web application.</summary>
    private readonly WebApplication _app;

    /// <summary>The certificate the server presents; kept so it is released with the server.</summary>
    private readonly X509Certificate2 _certificate;

    /// <summary>Every path asked for, in arrival order.</summary>
    private readonly ConcurrentQueue<string> _paths;

    /// <summary>Initializes a new instance of the <see cref="SelfSignedHttpsServer"/> class.</summary>
    /// <param name="app">The started application.</param>
    /// <param name="certificate">The certificate it presents.</param>
    /// <param name="paths">The queue the application writes the asked paths to.</param>
    /// <param name="port">The port it listens on.</param>
    private SelfSignedHttpsServer(WebApplication app, X509Certificate2 certificate, ConcurrentQueue<string> paths, int port)
    {
        _app = app;
        _certificate = certificate;
        _paths = paths;
        Port = port;
    }

    /// <summary>Gets the port the server listens on, on the loopback address.</summary>
    public int Port { get; }

    /// <summary>Gets the paths the server answered, in arrival order.</summary>
    public IReadOnlyList<string> Paths => [.. _paths];

    /// <summary>Starts a server whose certificate is issued to a host name nobody trusts the issuer of.</summary>
    /// <param name="hostName">The name the certificate is issued to.</param>
    /// <returns>The running server.</returns>
    public static async Task<SelfSignedHttpsServer> StartAsync(string hostName)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={hostName}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(hostName);
        request.CertificateExtensions.Add(names.Build());
        using var issued = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        // Kestrel needs a certificate whose private key it can use, which the exported and reloaded one is on every platform.
        var certificate = X509CertificateLoader.LoadPkcs12(issued.Export(X509ContentType.Pfx), null);

        var paths = new ConcurrentQueue<string>();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listen =>
        {
            listen.Protocols = HttpProtocols.Http1;
            listen.UseHttps(certificate);
        }));

        var app = builder.Build();
        app.Run(async context =>
        {
            paths.Enqueue(context.Request.Path.Value ?? string.Empty);
            await context.Response.WriteAsync("ok");
        });
        await app.StartAsync();

        var address = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First());
        return new SelfSignedHttpsServer(app, certificate, paths, address.Port);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
        _certificate.Dispose();
    }
}
