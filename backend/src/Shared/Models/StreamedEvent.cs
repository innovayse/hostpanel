namespace Innovayse.Hostpanel.Shared.Models;

/// <summary>One event of a streamed reply, before it is framed as a server-sent event.</summary>
/// <param name="Data">The event text; may span lines, each of which becomes a <c>data</c> line.</param>
/// <param name="Name">The event name written as the <c>event</c> line, or <see langword="null"/> for the default event; never contains a line break.</param>
/// <param name="Id">The event id written as the <c>id</c> line, or <see langword="null"/> for none; never contains a line break.</param>
public sealed record StreamedEvent(string Data, string? Name = null, string? Id = null);
