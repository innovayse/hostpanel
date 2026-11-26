using Innovayse.Hostpanel.Shared.Models.Enums;

namespace Innovayse.Hostpanel.Shared.Models;

/// <summary>The liveness probe's answer.</summary>
/// <param name="Status">What the probe reports about the process.</param>
/// <param name="Timestamp">When the process answered, UTC.</param>
/// <param name="ProcessStartedAt">
/// When this process started, UTC. It changes with every start, so a client that restarted the host knows the new process
/// is the one answering once the value differs from the one it read before.
/// </param>
public sealed record HealthDto(HealthStatus Status, DateTimeOffset Timestamp, DateTimeOffset ProcessStartedAt);
