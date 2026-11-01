using Innovayse.Hostpanel.Application.Common.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A host on a real loopback Kestrel socket that serves the <see cref="BodyProbe"/> probes with the largest body
/// cap a deployment may set (64 MiB, above the web server's own default). The in-memory test server has no per request body
/// limit, so what the web server does with a large, chunked or unread body can only be seen here. The production host
/// cannot be put on a socket by the test factory: the API's entry point runs the JasperFx command line, whose run command
/// disposes the host as soon as a real server starts.
/// </summary>
public sealed class KestrelBodyHost : IAsyncLifetime
{
    /// <summary>The running host.</summary>
    private WebApplication? _app;

    /// <summary>Gets the address the host listens on.</summary>
    public Uri Address { get; private set; } = null!;

    /// <summary>Starts the host on a loopback port the system chooses.</summary>
    /// <returns>A task that completes when the host is listening.</returns>
    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.Configure<PluginApiOptions>(options => options.MaxBodyBytes = PluginApiOptions.MaxBodyBytesCeiling);
        builder.Services.AddControllers().AddApplicationPart(typeof(BodyProbe).Assembly);

        _app = builder.Build();
        _app.MapControllers();
        await _app.StartAsync();

        Address = new Uri(_app.Urls.First());
    }

    /// <summary>Stops the host.</summary>
    /// <returns>A task that completes when the host has stopped.</returns>
    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    /// <summary>Makes a client of the host. A generated upload can take a while, so it waits longer than the default.</summary>
    /// <returns>The client, to dispose when the test ends.</returns>
    public HttpClient CreateClient()
    {
        return new HttpClient { BaseAddress = Address, Timeout = TimeSpan.FromMinutes(2) };
    }
}
