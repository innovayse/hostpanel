namespace Innovayse.Application.Support.Commands.DeleteAnnouncement;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>Handles <see cref="DeleteAnnouncementCommand"/> by permanently removing the announcement.</summary>
public sealed class DeleteAnnouncementHandler(IAnnouncementRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Deletes an announcement by its identifier.
    /// </summary>
    /// <param name="cmd">The delete announcement command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the announcement is not found.</exception>
    public async Task HandleAsync(DeleteAnnouncementCommand cmd, CancellationToken ct)
    {
        var announcement = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Announcement {cmd.Id} not found.");

        repo.Remove(announcement);
        await uow.SaveChangesAsync(ct);
    }
}
