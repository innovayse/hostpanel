/**
 * The one map from the kind of an action to its icon, so the same action reads the same on every screen.
 *
 * A button or link that acts (save, delete, add, ...) names its kind with the `action` prop of `UiButton` or `UiLink`
 * and the kit draws the icon from {@link actionIcons}; a screen never picks an icon for a common action by hand. An
 * action with no kind here passes its own `icon` (an `Icon` entry) instead.
 */
import { Icon } from '@/lib/icons'
import { UiButtonVariant } from '@/utils/ui/button'

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
  /** Creates a record. It is always the primary look (see {@link resolveActionVariant}), so every create button reads the same. */
  Add: 'add',
  /** Adds a line, an entry or a secondary record inside a larger whole (a row of a form, a time entry, a group); never primary. */
  AddRow: 'add-row',
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
  /** Opens the management of a record or a set (its editing screen, a list of groups). */
  Manage: 'manage',
  /** Brings records in from an outside source (a registrar, a provider). */
  Import: 'import',
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
  /** Goes back one step or one page within the same screen; never the way up to another page. */
  Previous: 'previous',
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
  [UiActionKind.Cancel]: Icon.Remove,
  [UiActionKind.Close]: Icon.Close,
  [UiActionKind.Delete]: Icon.Trash,
  [UiActionKind.Remove]: Icon.Remove,
  [UiActionKind.Add]: Icon.Add,
  [UiActionKind.AddRow]: Icon.Add,
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
  [UiActionKind.Manage]: Icon.Settings,
  [UiActionKind.Import]: Icon.Download,
  [UiActionKind.Open]: Icon.ExternalLink,
  [UiActionKind.View]: Icon.Eye,
  [UiActionKind.Test]: Icon.Zap,
  [UiActionKind.Send]: Icon.Send,
  [UiActionKind.Next]: Icon.ArrowRight,
  [UiActionKind.Previous]: Icon.ArrowLeft,
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

/**
 * The look a button or link draws: the primary look for a create action, the caller's own look for every other one.
 *
 * `UiActionKind.Add` is the create action and is always primary, so a call site cannot override it; an add that is
 * not the screen's main create (a row of a form) names `UiActionKind.AddRow` instead.
 *
 * @param variant - The look the caller asked for.
 * @param action - Kind of the action, if the caller named one.
 * @returns The look to draw.
 */
export const resolveActionVariant = (variant: UiButtonVariant, action: UiActionKind | undefined): UiButtonVariant =>
  action === UiActionKind.Add ? UiButtonVariant.Primary : variant

/** Classes of a row of action buttons on a phone: stacked, the primary action on top of the others, each as wide as the row. Dialog and drawer footers and form action bars share them. */
export const ACTION_ROW_PHONE_CLASSES = 'max-sm:flex-col-reverse max-sm:items-stretch'
