/**
 * The events of a preview frame's selection messages.
 *
 * @module types/pluginselectionevent
 */

/** What a selection message says. */
export enum PluginSelectionEvent {
  /** From the frame: its script runs. */
  Ready = 'ready',
  /** From the frame: the reader pointed at an element. */
  Toggle = 'toggle',
  /** From the frame: the reader asked to leave the mode. */
  Exit = 'exit',
  /** To the frame: the mode and what is selected. */
  State = 'state'
}
