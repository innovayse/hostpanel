namespace Innovayse.Application.Support.Queries.ListPublishedAnnouncements;

using Innovayse.Application.Support.Common;

/// <summary>
/// The announcement projection a client is allowed to see.
/// </summary>
/// <remarks>
/// Deliberately not <see cref="AnnouncementDto"/>. That record carries <c>IsPublished</c>, which
/// is an editorial fact about the row rather than news for a customer: on a client route it is
/// either always <see langword="true"/> and therefore noise, or -- worse -- evidence that a
/// draft pipeline exists. Only published rows reach this type, so the flag has nothing left to
/// say and is left out rather than pinned to a constant.
/// </remarks>
/// <param name="Id">The announcement identifier.</param>
/// <param name="Title">The announcement title.</param>
/// <param name="Content">The full announcement body content.</param>
/// <param name="PublishedAt">UTC timestamp the announcement was created, shown as its date.</param>
public record PublishedAnnouncementDto(int Id, string Title, string Content, DateTimeOffset PublishedAt);
