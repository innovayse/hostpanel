/**
 * The events a plugin chat stream sends: text as the plugin writes it, whole messages, status lines, a failure and
 * the end.
 *
 * @module types/pluginchateventtype
 */

/** An event type of a plugin chat stream. */
export enum PluginChatEventType {
  /** More text of the reply being written: `{ text }`. */
  Delta = 'delta',
  /** A whole reply: `{ text }`. */
  Message = 'message',
  /** What the plugin is doing now: `{ text }`. */
  Status = 'status',
  /** The plugin failed: `{ text }`, already in the reader's language. */
  Error = 'error',
  /** A link offered with the reply: `{ text, url }`, shown only when the link rule admits `url`. */
  Link = 'link',
  /** The reply is complete. */
  Done = 'done'
}
