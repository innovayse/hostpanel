using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Tells the caller of a plugin install, update, removal or restart how far the change has come, as it happens. The one
/// progress port every part of a change reports through (the handlers, the registry download, the package store and the
/// change applier). A caller that asked for a stream receives each report as a <c>progress</c> event; for any other
/// caller a report does nothing, so the change runs the same either way.
/// </summary>
public interface IPluginChangeProgress
{
    /// <summary>Reports that the change moved on to a stage.</summary>
    /// <param name="stage">The stage it is in now.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the report has been passed on.</returns>
    public Task EnterAsync(PluginChangeStage stage, CancellationToken ct);

    /// <summary>Reports how much of the package the download has read.</summary>
    /// <param name="bytesDone">The bytes read so far.</param>
    /// <param name="bytesTotal">The size the registry declared; <see langword="null"/> when it declared none.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the report has been passed on, or at once when it adds nothing new.</returns>
    public Task DownloadedAsync(long bytesDone, long? bytesTotal, CancellationToken ct);
}
