namespace Innovayse.Application.Support.Commands.CreateDownload;

using Innovayse.Application.Common;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Creates a new download entry.
/// </summary>
public sealed class CreateDownloadHandler(IDownloadRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="CreateDownloadCommand"/>.
    /// </summary>
    /// <param name="command">The create download command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ID of the newly created download.</returns>
    public async Task<int> HandleAsync(CreateDownloadCommand command, CancellationToken ct)
    {
        var download = Download.Create(
            command.Title,
            command.Description,
            command.Type,
            command.Filename,
            command.CategoryId,
            command.ClientsOnly,
            command.ProductDownload,
            command.IsHidden);

        repo.Add(download);
        await uow.SaveChangesAsync(ct);
        return download.Id;
    }
}
