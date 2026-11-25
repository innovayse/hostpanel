using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Storage;
using Innovayse.Hostpanel.Infrastructure.Storage.Settings;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Storage;

/// <summary>
/// Tests for the filesystem branding storage: one folder per upload under the kind's own directory, and a refused
/// write answered as a storage error rather than leaked as an authentication failure.
/// </summary>
public sealed class FileSystemBrandingStorageTests
{
    /// <summary>One primary rendition, enough to exercise a write.</summary>
    private static readonly BrandingRendition[] OneLogo =
    [
        new("logo.png", [1, 2, 3], "image/png", null, BrandingRole.Primary),
    ];

    /// <summary>Builds the storage over a web root.</summary>
    /// <param name="webRoot">The directory standing in for the web root.</param>
    /// <param name="logger">The logger to capture into.</param>
    /// <returns>The storage under test.</returns>
    private static FileSystemBrandingStorage Create(string webRoot, CapturingLogger<FileSystemBrandingStorage> logger) =>
        new(new StubWebHostEnvironment(webRoot), Options.Create(new BrandingOptions()), logger);

    /// <summary>Each slot is stored in its own directory, so the dark logo never lands in the light logo's folder.</summary>
    /// <param name="kind">The slot being stored.</param>
    /// <param name="segment">The directory its URL must carry.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(BrandingKind.Logo, "logo")]
    [InlineData(BrandingKind.LogoDark, "logodark")]
    [InlineData(BrandingKind.LogoMark, "logomark")]
    [InlineData(BrandingKind.Favicon, "favicon")]
    public async Task Save_StoresEachKindUnderItsOwnDirectoryAsync(BrandingKind kind, string segment)
    {
        var root = Path.Combine(Path.GetTempPath(), $"hostpanel-branding-{Guid.NewGuid():n}");
        try
        {
            var result = await Create(root, new CapturingLogger<FileSystemBrandingStorage>()).SaveAsync(kind, OneLogo, default);

            result.IsSuccess.Should().BeTrue();
            result.Value!.Single().Url.Should().StartWith($"/uploads/branding/{segment}/");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A folder the process may not write to answers <c>STORAGE_NOT_WRITABLE</c> and logs the cause as an error.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Save_AnUnwritableFolder_AnswersStorageNotWritableAsync()
    {
        if (!UnwritableUploadFolder.CanBeMadeUnwritable)
        {
            return;
        }

        using var folder = new UnwritableUploadFolder();
        var logger = new CapturingLogger<FileSystemBrandingStorage>();

        var result = await Create(folder.WebRoot, logger).SaveAsync(BrandingKind.LogoDark, OneLogo, default);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.STORAGE_NOT_WRITABLE);
        logger.Entries.Should().Contain(e => e.Level == LogLevel.Error && e.Message.Contains("not writable"));
    }

    /// <summary>
    /// Removing a superseded upload from a folder that refuses the delete does not fail the caller: the new value is
    /// already saved, so the refusal is only logged.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Remove_AnUnwritableFolder_LogsAndDoesNotThrowAsync()
    {
        if (!UnwritableUploadFolder.CanBeMadeUnwritable)
        {
            return;
        }

        using var folder = new UnwritableUploadFolder();
        var logger = new CapturingLogger<FileSystemBrandingStorage>();

        var act = () => Create(folder.WebRoot, logger)
            .RemoveAsync($"/uploads/branding/logo/{folder.ExistingUploadId}/logo.png", default);

        await act.Should().NotThrowAsync();
        logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Message.Contains("not writable"));
    }
}
