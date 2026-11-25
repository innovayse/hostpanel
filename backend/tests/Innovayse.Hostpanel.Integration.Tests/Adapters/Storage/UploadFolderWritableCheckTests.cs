using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Innovayse.Hostpanel.Infrastructure.Storage;
using Innovayse.Hostpanel.Infrastructure.Storage.Settings;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Storage;

/// <summary>Tests for the startup check that reports an unwritable upload, plugin or log folder.</summary>
public sealed class UploadFolderWritableCheckTests
{
    /// <summary>Builds the check over a web root and a plugin folder.</summary>
    /// <param name="webRoot">The directory standing in for the web root.</param>
    /// <param name="pluginFolder">The absolute folder uploaded plugins are unpacked into.</param>
    /// <param name="logger">The logger to capture into.</param>
    /// <returns>The check under test.</returns>
    private static UploadFolderWritableCheck Create(
        string webRoot, string pluginFolder, CapturingLogger<UploadFolderWritableCheck> logger) =>
        new(
            new StubWebHostEnvironment(webRoot),
            Options.Create(new BrandingOptions()),
            Options.Create(new PluginOptions { InstallPath = pluginFolder }),
            logger);

    /// <summary>Writable folders pass, are created when missing and leave no probe file behind.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task WritableFolders_PassAndLeaveNothingBehindAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), $"hostpanel-check-{Guid.NewGuid():n}");
        try
        {
            var logger = new CapturingLogger<UploadFolderWritableCheck>();
            var check = Create(root, Path.Combine(root, "plugins-installed"), logger);

            await check.StartAsync(default);

            check.UnwritableFolders().Should().BeEmpty();
            Directory.GetFiles(Path.Combine(root, "uploads", "branding")).Should().BeEmpty();
            Directory.GetFiles(Path.Combine(root, "plugins-installed")).Should().BeEmpty();
            logger.Entries.Should().NotContain(e => e.Level == LogLevel.Error);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>An unwritable branding folder is reported as an error at start, and start still completes.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UnwritableBrandingFolder_IsReportedAsAnErrorAndStartCompletesAsync()
    {
        if (!UnwritableUploadFolder.CanBeMadeUnwritable)
        {
            return;
        }

        using var folder = new UnwritableUploadFolder();
        var plugins = Path.Combine(folder.WebRoot, "plugins-installed");
        var logger = new CapturingLogger<UploadFolderWritableCheck>();
        var check = Create(folder.WebRoot, plugins, logger);

        await check.StartAsync(default);

        logger.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Message.Contains("not writable"));
        check.UnwritableFolders().Should().ContainSingle().Which.Should().EndWith(Path.Combine("uploads", "branding"));
    }

    /// <summary>An unwritable plugin folder is reported on its own, while the branding folder still passes.</summary>
    [Fact]
    public void UnwritablePluginFolder_IsReportedAlone()
    {
        if (!UnwritableUploadFolder.CanBeMadeUnwritable)
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), $"hostpanel-plugins-{Guid.NewGuid():n}");
        var plugins = Path.Combine(root, "plugins-installed");
        Directory.CreateDirectory(plugins);
        File.SetUnixFileMode(plugins, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var check = Create(root, plugins, new CapturingLogger<UploadFolderWritableCheck>());

            check.UnwritableFolders().Should().ContainSingle().Which.Should().Be(plugins);
        }
        finally
        {
            File.SetUnixFileMode(plugins, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.Delete(root, recursive: true);
        }
    }
}
