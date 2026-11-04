namespace Innovayse.Hostpanel.Shared.Models;

/// <summary>A reply written as a stream of server-sent events instead of one body.</summary>
/// <param name="Events">The events, produced as they become available; enumerated by the response, with the request abort token.</param>
/// <param name="Heartbeat">How long the stream may stay silent before a comment line keeps the connection alive.</param>
/// <param name="IdleLimit">How long the producer may stay silent before the response ends the stream; <see langword="null"/> for no limit.</param>
/// <param name="Owner">
/// What the producer needs for as long as it runs (a dependency scope with its own data context), disposed by the response
/// once the producer is over: after the stream ended, failed or the client left, and never while a step of the producer is
/// still running. <see langword="null"/> when the producer holds nothing of its own.
/// </param>
public sealed record StreamedReply(
    IAsyncEnumerable<StreamedEvent> Events, TimeSpan Heartbeat, StreamIdleLimit? IdleLimit = null, IAsyncDisposable? Owner = null);
