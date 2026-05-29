namespace Innovayse.Application.Support.Queries.GetAnnouncement;

using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Loads a single announcement and maps it to <see cref="AnnouncementDto"/>.
/// </summary>
public sealed class GetAnnouncementHandler(IAnnouncementRepository repo)
{
    /// <summary>
    /// Handles <see cref="GetAnnouncementQuery"/>.
    /// </summary>
    /// <param name="query">The get announcement query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="AnnouncementDto"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the announcement is not found.</exception>
    public async Task<AnnouncementDto> HandleAsync(GetAnnouncementQuery query, CancellationToken ct)
    {
        var announcement = await repo.FindByIdAsync(query.Id, ct)
            ?? throw new InvalidOperationException($"Announcement {query.Id} not found.");

        return new AnnouncementDto(
            announcement.Id,
            announcement.Title,
            announcement.Content,
            announcement.IsPublished,
            announcement.CreatedAt);
    }
}
