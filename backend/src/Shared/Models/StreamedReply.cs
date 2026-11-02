namespace Innovayse.Hostpanel.Shared.Models;

/// <summary>A reply written as a stream of server-sent events instead of one body.</summary>
/// <param name="Events">The events, produced as they become available; enumerated by the response, with the request abort token.</param>
/// <param name="Heartbeat">How long the stream may stay silent before a comment line keeps the connection alive.</param>
public sealed record StreamedReply(IAsyncEnumerable<StreamedEvent> Events, TimeSpan Heartbeat);
