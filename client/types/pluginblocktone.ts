/**
 * The tones a plugin view may give an alert, a badge or an action.
 *
 * @module types/pluginblocktone
 */

/** A tone of a plugin block. */
export enum PluginBlockTone {
  /** Neutral. */
  Neutral = 'neutral',
  /** Information. */
  Info = 'info',
  /** Success. */
  Success = 'success',
  /** A warning. */
  Warning = 'warning',
  /** A failure or a destructive action. */
  Danger = 'danger',
  /** The primary action. */
  Primary = 'primary'
}
