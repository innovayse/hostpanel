using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Integrations.Migration.Settings;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Migration;

/// <summary>
/// <c>GET /api/migrations/plugin/download</c>, dispatching <c>DownloadMigrationPluginQuery</c>.
/// Public and anonymous: the legacy system being migrated away from has no credential this
/// platform would recognise when it fetches its importer plugin.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DownloadMigrationPluginTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The folder every archive entry sits under, relative to the source system's root.</summary>
    private const string AddonRoot = "modules/addons/innovayse_migration/";

    /// <summary>The download route.</summary>
    private const string DownloadUrl = "/api/migrations/plugin/download";

    /// <summary>
    /// No credential is required, and the build output carries the plugin under the default
    /// <c>MigrationPlugin:Path</c>, so the answer is the zip archive in the installable layout.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DownloadPlugin_IsAnonymousAndAnswersTheZipAsync()
    {
        var response = await factory.CreateClient().GetAsync(DownloadUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/zip");
        await AssertInstallableLayoutAsync(response);
    }

    /// <summary>
    /// When <c>MigrationPlugin:Path</c> names a folder that does not exist, the download is the
    /// coded 404 refusal with a sentence, never an unmapped fault.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DownloadPlugin_WithTheFolderMissing_AnswersTheCodedRefusalAsync()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"missing-migration-plugin-{Guid.NewGuid():N}");
        await using var host = factory.WithWebHostBuilder(builder => builder.UseSetting(
            $"{MigrationPluginOptions.SectionName}:{nameof(MigrationPluginOptions.Path)}", missing));

        var response = await host.CreateClient().GetAsync(DownloadUrl);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.MIGRATION_PLUGIN_NOT_FOUND);
        body.RootElement.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>A blank <c>MigrationPlugin:Path</c> is refused when the API starts, naming the setting.</summary>
    [Fact]
    public void Startup_RefusesABlankPath()
    {
        var key = $"{MigrationPluginOptions.SectionName}:{nameof(MigrationPluginOptions.Path)}";
        using var host = factory.WithWebHostBuilder(builder => builder.UseSetting(key, " "));

        var act = () => host.CreateClient();

        act.Should().Throw<OptionsValidationException>().WithMessage($"*{key}*");
    }

    /// <summary>
    /// The archive holds the addon exactly as the source system's root expects it: every entry
    /// under <c>modules/addons/innovayse_migration/</c>, the two entry points and the class
    /// library among them, and none of the plugin's tests or development files.
    /// </summary>
    /// <param name="response">The successful download.</param>
    /// <returns>A task that completes when the archive has been checked.</returns>
    private static async Task AssertInstallableLayoutAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var entries = zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();

        entries.Should().OnlyContain(name => name.StartsWith(AddonRoot, StringComparison.Ordinal));
        entries.Should().Contain(new[]
        {
            $"{AddonRoot}api.php",
            $"{AddonRoot}innovayse_migration.php",
            $"{AddonRoot}autoload.php",
            $"{AddonRoot}lib/ApiKernel.php",
        });
        entries.Should().NotContain(name =>
            name.Contains("/tests/", StringComparison.Ordinal)
            || name.EndsWith("composer.json", StringComparison.Ordinal)
            || name.EndsWith("phpunit.xml.dist", StringComparison.Ordinal)
            || name.Contains("/vendor/", StringComparison.Ordinal));
    }
}
