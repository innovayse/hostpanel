namespace Innovayse.Application.Migration.Commands.PingMigrationJob;

/// <summary>Called by the migration plugin to confirm it is connected. Updates LastPingAt.</summary>
/// <param name="Key">The migration job's secret key.</param>
public sealed record PingMigrationJobCommand(string Key);
