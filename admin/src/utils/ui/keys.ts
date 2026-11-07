/**
 * Keyboard keys the kit and the screens react to, named once. The values are the `KeyboardEvent.key`
 * strings the browser reports.
 */

/** Closed set of the keys a handler listens for. */
export enum Key {
  /** Confirms or submits. */
  Enter = 'Enter',
  /** Closes the surface on top. */
  Escape = 'Escape',
  /** Moves focus to the next control. */
  Tab = 'Tab',
  /** Moves to the previous item. */
  ArrowLeft = 'ArrowLeft',
  /** Moves to the next item. */
  ArrowRight = 'ArrowRight',
  /** Moves to the first item. */
  Home = 'Home',
  /** Moves to the last item. */
  End = 'End',
}
