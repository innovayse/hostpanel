/**
 * The one place a store turns a rejected API call into its own `error` state.
 *
 * A view never holds a `catch`: a store action runs its request through {@link runAction},
 * which writes the API's own sentence (see `errorHandler.ts`) into the store's `error`
 * ref and reports the outcome as a value the view can branch on.
 */

import type { Ref } from 'vue'
import { errorSentence, fieldErrors as fieldErrorsOf } from '@/lib/errorHandler'

/** Outcome of {@link runAction}: the resolved value, or a failure whose sentence is already in the store's error ref. */
export type ActionResult<T> = { ok: true; value: T } | { ok: false }

/**
 * Runs one store request, capturing a failure into the store's error state.
 *
 * @param error - The store's error ref; cleared before the call, set to the sentence on failure.
 * @param task - The request to run.
 * @param fieldErrors - Optional ref that receives the API's per-field messages (empty on success).
 * @returns `{ ok: true, value }` on success, `{ ok: false }` on failure.
 */
export const runAction = async <T>(
  error: Ref<string | null>,
  task: () => Promise<T>,
  fieldErrors?: Ref<Record<string, string>>,
): Promise<ActionResult<T>> => {
  error.value = null
  if (fieldErrors) {
    fieldErrors.value = {}
  }
  try {
    return { ok: true, value: await task() }
  } catch (cause) {
    error.value = errorSentence(cause)
    if (fieldErrors) {
      fieldErrors.value = fieldErrorsOf(cause)
    }
    return { ok: false }
  }
}

/**
 * Removes one field's message from a store's field errors, leaving the other fields' messages.
 *
 * The map is replaced rather than mutated so watchers of the ref see the change.
 *
 * @param fieldErrors - The store's field errors ref.
 * @param field - The wire name of the field whose message goes.
 * @returns Nothing.
 */
export const dropFieldError = (fieldErrors: Ref<Record<string, string>>, field: string): void => {
  const rest = { ...fieldErrors.value }
  delete rest[field]
  fieldErrors.value = rest
}
