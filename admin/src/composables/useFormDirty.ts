/** Composable that tracks whether a form holds edits that are not saved yet. */
import { nextTick, ref, watch } from 'vue'
import type { Ref, WatchSource } from 'vue'

/** What {@link useFormDirty} hands back to the form. */
export interface FormDirty {
  /** True once the watched state changed after the last {@link FormDirty.settle}. */
  isDirty: Ref<boolean>
  /** Declares the current state the saved one; resolves once the pending watcher run is over. */
  settle: () => Promise<void>
}

/**
 * Tracks edits of a form's reactive state.
 *
 * A watcher runs on the next flush, not at the moment its source changes, so a flag cleared
 * synchronously after populating the form is raised again by the very writes that populated it.
 * {@link FormDirty.settle} therefore waits for that flush (including watchers that write into
 * other watched state, such as a product link filling its URL) before it clears the flag, which
 * makes loading a stored entry, or the end of a save, never count as an edit.
 *
 * @param source - The reactive state (or the list of refs and reactive objects) whose deep changes are edits.
 * @returns The dirty flag and the way to declare the current state saved.
 */
export const useFormDirty = (source: object | WatchSource | WatchSource[]): FormDirty => {
  /** Whether the watched state changed since the last settle. */
  const isDirty = ref(false)

  watch(source as WatchSource | WatchSource[], () => {
    isDirty.value = true
  }, { deep: true })

  /**
   * Clears the flag after the writes made so far have been seen by the watcher.
   *
   * @returns Resolves once the flag is clear.
   */
  const settle = async (): Promise<void> => {
    await nextTick()
    isDirty.value = false
  }

  return { isDirty, settle }
}
