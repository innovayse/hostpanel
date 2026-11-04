/**
 * How a plugin view formats a value bound from a route: with the host's locale formatters, never as markup.
 *
 * @module types/pluginvalueformat
 */

/** A value format of a plugin view. */
export enum PluginValueFormat {
  /** The value as text. */
  Text = 'text',
  /** An ISO date and time, in the reader's locale. */
  DateTime = 'datetime',
  /** `{ amount, currency }`, in the reader's locale. */
  Money = 'money',
  /** A short status word drawn as a badge. */
  Badge = 'badge'
}
