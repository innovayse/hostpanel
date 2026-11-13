/**
 * The rules behind the kit's listbox (`UiSelect` and the pickers built on it): the shape of one
 * option, how a search term filters, how the arrow keys move the highlighted row and how typing on
 * a closed-search list jumps to a label. Pure functions, so the keyboard contract is tested without
 * a DOM.
 */
import { Key } from '@/utils/ui/keys'

/** One row of a listbox. */
export interface UiSelectOption<T extends string | number | null> {
  /** The value the model takes when the row is picked. */
  value: T
  /** Text of the row, shown on the trigger once picked and matched by the search. */
  label: string
  /** Longer text for the row in the list; the trigger keeps `label`. */
  displayLabel?: string
  /** Small line above the row text (a status). */
  caption?: string
  /** Quiet line under the row text (an email, a code). */
  description?: string
  /** Extra text the search matches without showing it (an id, an alias). */
  keywords?: string
  /** Draws the row and, once picked, the trigger dim: a "none" or "all" choice. */
  muted?: boolean
}

/** Longest pause between two keystrokes that still count as one typed word, in milliseconds. */
export const TYPEAHEAD_RESET_MS = 700

/**
 * Keeps the options that match a search term in their label, long label, description or keywords.
 * A dim "none" choice (a `muted` option) is not an answer to a term, so it is dropped as soon as one is typed.
 *
 * @param options - Every option.
 * @param term - What the reader typed; blank keeps everything.
 * @param matchText - False when the owner already matched the term (a remote list): only the dim rows go.
 * @returns The remaining options, in their original order.
 */
export const filterOptions = <T extends string | number | null>(
  options: readonly UiSelectOption<T>[],
  term: string,
  matchText = true,
): UiSelectOption<T>[] => {
  const needle = term.trim().toLowerCase()
  if (!needle) {
    return [...options]
  }
  return options.filter(option =>
    !option.muted
    && (!matchText || [option.label, option.displayLabel, option.description, option.keywords]
      .some(text => text?.toLowerCase().includes(needle))),
  )
}

/**
 * Moves the highlighted row for a navigation key. The highlight stops at both ends, as in a native
 * select.
 *
 * @param current - Index of the highlighted row, -1 for none.
 * @param count - Number of rows.
 * @param key - The pressed key.
 * @returns The new index, -1 when there are no rows, or the same index for a key that does not move.
 */
export const stepActive = (current: number, count: number, key: string): number => {
  if (count === 0) {
    return -1
  }
  switch (key) {
    case Key.ArrowDown:
      return Math.min(current + 1, count - 1)
    case Key.ArrowUp:
      return Math.max(current - 1, 0)
    case Key.Home:
      return 0
    case Key.End:
      return count - 1
    default:
      return current
  }
}

/**
 * Whether a key press types a character (as opposed to a control or a shortcut).
 *
 * @param event - The key event.
 * @returns True for one printable character without Ctrl, Alt or Meta.
 */
export const isPrintableKey = (event: Pick<KeyboardEvent, 'key' | 'ctrlKey' | 'altKey' | 'metaKey'>): boolean =>
  event.key.length === 1 && !event.ctrlKey && !event.altKey && !event.metaKey

/**
 * Finds the row a typed word points at: the first row after `from` whose label starts with the word,
 * wrapping round to the top.
 *
 * @param labels - Row labels in list order.
 * @param word - Characters typed so far.
 * @param from - Index to start after, -1 to start at the top.
 * @returns The index of the match, or -1 when no label starts with the word.
 */
export const typeaheadIndex = (labels: readonly string[], word: string, from: number): number => {
  const needle = word.toLowerCase()
  for (let step = 1; step <= labels.length; step++) {
    const index = (from + step + labels.length) % labels.length
    if (labels[index]?.toLowerCase().startsWith(needle)) {
      return index
    }
  }
  return -1
}

/**
 * Builds the DOM id of one row, so the trigger can name it in `aria-activedescendant`.
 *
 * @param listId - Id of the listbox.
 * @param index - Index of the row.
 * @returns The row's id.
 */
export const optionDomId = (listId: string, index: number): string => `${listId}-option-${index}`
