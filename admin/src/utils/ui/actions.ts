/**
 * The one map from the kind of an action to its icon, so the same action reads the same on every screen.
 *
 * A button or link that acts (save, delete, add, ...) names its kind with the `action` prop of `UiButton` or `UiLink`
 * and the kit draws the icon from {@link actionIcons}; a screen never picks an icon for a common action by hand. An
 * action with no kind here passes its own `icon` (an `Icon` entry) instead.
 */
import { Icon } from '@/lib/icons'

/** Closed set of action kinds. */
export const UiActionKind = {
  /** Writes the screen's changes. */
  Save: 'save',
  /** Leaves without writing. */
  Cancel: 'cancel',
  /** Dismisses a message or a dialog. */
  Close: 'close',
  /** Removes a record for good. */
  Delete: 'delete',
  /** Takes a part out of a larger whole (a line, a component), the whole staying. */
  Remove: 'remove',
  /** Adds or creates a record. */
  Add: 'add',
  /** Opens a record for change. */
  Edit: 'edit',
  /** Reads again, renews or retries. */
  Refresh: 'refresh',
  /** Takes data out as a file. */
  Export: 'export',
  /** Fetches a file. */
  Download: 'download',
  /** Sends a file in. */
  Upload: 'upload',
  /** Prints the screen. */
  Print: 'print',
  /** Starts a report or builds a result. */
  Generate: 'generate',
  /** Puts the chosen filters or values to work. */
  Apply: 'apply',
  /** Empties a filter or a field. */
  Clear: 'clear',
  /** Undoes the edits made on the screen. */
  Reset: 'reset',
  /** Confirms a step or a choice. */
  Confirm: 'confirm',
  /** Opens the settings of a thing. */
  Configure: 'configure',
  /** Opens a thing outside the panel or in its own screen. */
  Open: 'open',
  /** Shows a record or a preview. */
  View: 'view',
  /** Checks that something works. */
  Test: 'test',
  /** Sends a message or a reply. */
  Send: 'send',
  /** Moves on to the next step. */
  Next: 'next',
  /** Makes a copy. */
  Copy: 'copy',
  /** Searches. */
  Search: 'search',
  /** Stops something running. */
  Stop: 'stop',
} as const

/** One kind of action. */
export type UiActionKind = (typeof UiActionKind)[keyof typeof UiActionKind]

/** The icon of each kind of action; the only place a common action is given its icon. */
export const actionIcons: Record<UiActionKind, string> = {
  [UiActionKind.Save]: Icon.Check,
  [UiActionKind.Cancel]: Icon.Undo,
  [UiActionKind.Close]: Icon.Close,
  [UiActionKind.Delete]: Icon.Trash,
  [UiActionKind.Remove]: Icon.Remove,
  [UiActionKind.Add]: Icon.Add,
  [UiActionKind.Edit]: Icon.Edit,
  [UiActionKind.Refresh]: Icon.Refresh,
  [UiActionKind.Export]: Icon.Download,
  [UiActionKind.Download]: Icon.Download,
  [UiActionKind.Upload]: Icon.Upload,
  [UiActionKind.Print]: Icon.Printer,
  [UiActionKind.Generate]: Icon.Play,
  [UiActionKind.Apply]: Icon.Check,
  [UiActionKind.Clear]: Icon.Close,
  [UiActionKind.Reset]: Icon.Undo,
  [UiActionKind.Confirm]: Icon.CheckCircle,
  [UiActionKind.Configure]: Icon.Settings,
  [UiActionKind.Open]: Icon.ExternalLink,
  [UiActionKind.View]: Icon.Eye,
  [UiActionKind.Test]: Icon.Zap,
  [UiActionKind.Send]: Icon.Send,
  [UiActionKind.Next]: Icon.ArrowRight,
  [UiActionKind.Copy]: Icon.Copy,
  [UiActionKind.Search]: Icon.Search,
  [UiActionKind.Stop]: Icon.Stop,
}

/**
 * The icon a button or link draws: its own `icon` when it has one, else the icon of its action kind.
 *
 * @param icon - Icon markup the caller passed, if any.
 * @param action - Kind of the action, if the caller named one.
 * @returns The icon markup, or undefined when the control carries neither.
 */
export const resolveActionIcon = (icon: string | undefined, action: UiActionKind | undefined): string | undefined =>
  icon ?? (action === undefined ? undefined : actionIcons[action])

/** Classes of a row of action buttons on a phone: stacked, the primary action on top of the others, each as wide as the row. Dialog and drawer footers and form action bars share them. */
export const ACTION_ROW_PHONE_CLASSES = 'max-sm:flex-col-reverse max-sm:items-stretch'
