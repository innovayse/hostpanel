namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The migration plugin this build ships for the source billing system, packaged for download.
/// Implemented in Infrastructure: finding the plugin's files and zipping them is file system work
/// the Application layer never does itself.
/// </summary>
public interface IMigrationPluginArchive
{
    /// <summary>
    /// Zips the installable part of the plugin: the module folder the source system loads. The
    /// plugin's own tests and development files stay out of the archive.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The archive, or null when this deployment does not carry the plugin's files.</returns>
    public Task<FileDownloadDto?> CreateAsync(CancellationToken ct);
}
