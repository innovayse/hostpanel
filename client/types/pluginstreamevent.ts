/**
 * One server-sent event of a plugin stream: its `event:` name and its `data:` parsed as JSON (or the raw text when
 * it is not JSON).
 *
 * @module types/pluginstreamevent
 */

/** One event of a plugin stream. */
export interface PluginStreamEvent {
  /** The event name, e.g. `delta`; `message` when the stream named none. */
  type: string
  /** The event's data. */
  data: unknown
}
