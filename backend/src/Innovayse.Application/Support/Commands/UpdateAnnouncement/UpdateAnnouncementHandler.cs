namespace Innovayse.Application.Support.Commands.UpdateAnnouncement;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>Handles <see cref="UpdateAnnouncementCommand"/> by updating the announcement fields.</summary>
public sealed class UpdateAnnouncementHandler(IAnnouncementRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Updates an existing announcement's title, content, and published state.
    /// </summary>
    /// <param name="cmd">The update announcement command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the announcement is not found.</exception>
    public async Task HandleAsync(UpdateAnnouncementCommand cmd, CancellationToken ct)
    {
        var announcement = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Announcement {cmd.Id} not found.");

        announcement.Update(cmd.Title, cmd.Content, cmd.IsPublished);
        await uow.SaveChangesAsync(ct);
    }
}
