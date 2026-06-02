namespace Innovayse.Application.Support.Commands.DeleteDownload;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Deletes a download entry.
/// </summary>
public sealed class DeleteDownloadHandler(IDownloadRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="DeleteDownloadCommand"/>.
    /// </summary>
    /// <param name="command">The delete download command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the download is not found.</exception>
    public async Task HandleAsync(DeleteDownloadCommand command, CancellationToken ct)
    {
        var download = await repo.FindByIdAsync(command.Id, ct)
            ?? throw new InvalidOperationException($"Download {command.Id} not found.");

        repo.Remove(download);
        await uow.SaveChangesAsync(ct);
    }
}
