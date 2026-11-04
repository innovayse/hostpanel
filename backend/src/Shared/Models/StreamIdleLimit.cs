namespace Innovayse.Hostpanel.Shared.Models;

/// <summary>
/// How long a streamed reply may go without an event of its producer before the response ends it. The host's own heartbeat
/// is not an event of the producer, so a stream that keeps sending is never cut and a silent one always is.
/// </summary>
/// <param name="Timeout">The longest silence of the producer, from the start of the stream or from its last event.</param>
/// <param name="EndEvents">The events written, after the producer was cancelled, to tell the client why the stream ended.</param>
/// <param name="OnExpired">Called once when the silence passed the limit, so the owner of the stream can log it.</param>
public sealed record StreamIdleLimit(TimeSpan Timeout, IReadOnlyList<StreamedEvent> EndEvents, Action OnExpired);
