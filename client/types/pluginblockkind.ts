/**
 * The block kinds of plugin view schema 1 (`PluginViews.BlockKinds` in the SDK). The host renders each with its own
 * component; a kind it does not know is skipped.
 *
 * @module types/pluginblockkind
 */

/** A block kind of a plugin view. */
export enum PluginBlockKind {
  /** A title with optional actions. */
  Header = 'header',
  /** Plain text, never HTML. */
  Text = 'text',
  /** A labelled value. */
  Stat = 'stat',
  /** Rows from a route, with columns and paging. */
  Table = 'table',
  /** Fields sent to a route. */
  Form = 'form',
  /** A button that calls a route. */
  Action = 'action',
  /** A link to one of the plugin's allowed origins. */
  Link = 'link',
  /** A short status word. */
  Badge = 'badge',
  /** An empty state. */
  Empty = 'empty',
  /** A notice. */
  Alert = 'alert',
  /** Tabs, each holding blocks. */
  Tabs = 'tabs',
  /** A chat streamed from a route. */
  Chat = 'chat',
  /** A progress bar. */
  Progress = 'progress'
}
