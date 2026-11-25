namespace Innovayse.Hostpanel.Application.Admin.Commands.MarkActivityRead;

/// <summary>Marks one event of the notification feed as read for the calling admin. Marking an event that is already read succeeds and changes nothing.</summary>
/// <param name="EventKey">The key of the event, as the feed returned it (<c>ActivityEventDto.Key</c>).</param>
public sealed record MarkActivityReadCommand(string EventKey);
