/**
 * The states of one step of a plugin chat reply.
 *
 * @module types/pluginchatstepstate
 */

/** A step's state. */
export enum PluginChatStepState {
  /** The plugin is doing it now. */
  Running = 'running',
  /** It finished. */
  Done = 'done',
  /** It failed. */
  Failed = 'failed',
  /** The reader stopped the reply while it ran; set by the panel, never read from a stream. */
  Stopped = 'stopped'
}
