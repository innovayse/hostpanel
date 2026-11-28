/**
 * The kinds of repeated read the panel runs on a timer. Every one of them is driven by `usePoller`; the kind decides
 * the interval (see `POLL_INTERVALS_MS`), so no screen holds a timer or a number of its own.
 */

/** What a poller reads, and so how often it asks. */
export enum PollKind {
  /** A running migration job's status. */
  MigrationStatus = 'MigrationStatus',
  /** The liveness probe while the server restarts after a plugin change. */
  ServerRestart = 'ServerRestart',
  /** The condition a plugin action or form follows up on. */
  PluginFollowUp = 'PluginFollowUp',
  /** The clock the plugin processing dialog counts its elapsed time on. */
  ProcessingClock = 'ProcessingClock',
  /** The clock the demo banner counts down on. */
  DemoCountdown = 'DemoCountdown',
}
