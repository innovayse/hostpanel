namespace Innovayse.Application.Support.Commands.UpdateDownload;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Updates an existing download entry.
/// </summary>
public sealed class UpdateDownloadHandler(IDownloadRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="UpdateDownloadCommand"/>.
    /// </summary>
    /// <param name="command">The update download command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the download is not found.</exception>
    public async Task HandleAsync(UpdateDownloadCommand command, CancellationToken ct)
    {
        var download = await repo.FindByIdAsync(command.Id, ct)
            ?? throw new InvalidOperationException($"Download {command.Id} not found.");

        download.Update(
            command.Title,
            command.Description,
            command.Type,
            command.Filename,
            command.CategoryId,
            command.ClientsOnly,
            command.ProductDownload,
            command.IsHidden);

        await uow.SaveChangesAsync(ct);
    }
}
