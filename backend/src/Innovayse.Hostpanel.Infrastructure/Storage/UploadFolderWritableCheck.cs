using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Innovayse.Hostpanel.Infrastructure.Storage.Extensions;
using Innovayse.Hostpanel.Infrastructure.Storage.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Storage;

/// <summary>
/// At startup, proves the host process can write to every folder it stores files in (branding uploads, uploaded plugins, logs) and reports each one it cannot, loudly.
/// </summary>
/// <remarks>
/// A volume created by an older image belongs to root while the host runs unprivileged, and nothing fails until an
/// operator uploads a logo or installs a plugin. The check surfaces that at boot, in the log an operator reads first. It does not stop the
/// host and it is not part of the liveness probe: an unwritable folder breaks one admin screen, and a failing
/// liveness probe would restart a healthy host in a loop without fixing the volume.
/// </remarks>
public sealed class UploadFolderWritableCheck : IHostedService
{
    /// <summary>Host environment, for locating the web root.</summary>
    private readonly IWebHostEnvironment _env;

    /// <summary>Resolved branding paths.</summary>
    private readonly BrandingOptions _options;

    /// <summary>Resolved plugin paths.</summary>
    private readonly PluginOptions _plugins;

    /// <summary>Log sink.</summary>
    private readonly ILogger<UploadFolderWritableCheck> _logger;

    /// <summary>Initializes a new instance of the <see cref="UploadFolderWritableCheck"/> class.</summary>
    /// <param name="env">Host environment, for locating the web root.</param>
    /// <param name="options">Branding paths.</param>
    /// <param name="plugins">Plugin paths.</param>
    /// <param name="logger">Log sink.</param>
    public UploadFolderWritableCheck(
        IWebHostEnvironment env,
        IOptions<BrandingOptions> options,
        IOptions<PluginOptions> plugins,
        ILogger<UploadFolderWritableCheck> logger)
    {
        _env = env;
        _options = options.Value;
        _plugins = plugins.Value;
        _logger = logger;
    }

    /// <summary>The folders the host writes to: the branding uploads, the unpacked plugins and the rolling logs.</summary>
    /// <returns>Their absolute paths.</returns>
    private IReadOnlyList<string> Folders() =>
        [_options.UploadsRoot(_env), _plugins.InstallRoot, Path.Combine(AppContext.BaseDirectory, "logs")];

    /// <summary>Tries to create a file in each folder and delete it; every refusal is logged as an error.</summary>
    /// <returns>The folders the host process may not write to; empty when all are writable.</returns>
    public IReadOnlyList<string> UnwritableFolders()
    {
        var refused = new List<string>();
        foreach (var folder in Folders())
        {
            try
            {
                Directory.CreateDirectory(folder);
                var probe = Path.Combine(folder, $".write-check-{Guid.NewGuid():n}");
                File.WriteAllBytes(probe, []);
                File.Delete(probe);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(
                    ex,
                    "The folder {Directory} is not writable by the host process, so what is stored there (uploaded logos, plugins, logs) will fail until the volume mounted there is owned by the app user.",
                    folder);
                refused.Add(folder);
            }
        }

        return refused;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = UnwritableFolders();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
