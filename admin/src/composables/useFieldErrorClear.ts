/**
 * Clears the API's message for a form field the moment the operator edits that field.
 *
 * Admin forms show the API's per-field messages under their inputs. Each form used to keep one
 * watch per field that emitted a clear event when its field had a message; this holds that once.
 */
import { watch } from 'vue'
import type { WatchSource } from 'vue'

/** One watched field: its wire name (the key of its API message) and the source that changes when it is edited. */
export type FieldErrorSource = readonly [field: string, source: WatchSource]

/**
 * Watches the given field sources and reports the field to clear when one changes while it holds a message.
 *
 * @param fieldErrors - Getter of the current API messages keyed by wire name.
 * @param clear - Called with the wire name of an edited field that has a message.
 * @param sources - The fields to watch, each as `[wireName, source]`.
 * @returns Nothing; the watchers stop with the calling component.
 */
export const useFieldErrorClear = (
  fieldErrors: () => Record<string, string>,
  clear: (field: string) => void,
  sources: readonly FieldErrorSource[],
): void => {
  for (const [field, source] of sources) {
    watch(source, () => {
      if (fieldErrors()[field] !== undefined) {
        clear(field)
      }
    })
  }
}
