namespace Innovayse.Application.Support.Commands.CreateAnnouncement;

using Innovayse.Application.Common;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;

/// <summary>Handles <see cref="CreateAnnouncementCommand"/> by creating a new announcement.</summary>
public sealed class CreateAnnouncementHandler(IAnnouncementRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Creates a new announcement and returns its identifier.
    /// </summary>
    /// <param name="cmd">The create announcement command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The identifier of the newly created announcement.</returns>
    public async Task<int> HandleAsync(CreateAnnouncementCommand cmd, CancellationToken ct)
    {
        var announcement = Announcement.Create(cmd.Title, cmd.Content, cmd.IsPublished);
        repo.Add(announcement);
        await uow.SaveChangesAsync(ct);
        return announcement.Id;
    }
}
