/**
 * The outcome line of a screen: how the last action went.
 *
 * The toast (`stores/toast.ts`) is the one channel for the result of an action: a success is a success toast and a
 * refusal is a danger toast carrying the API's own sentence. A toast is fixed on the window and drawn above dialogs, so
 * the reader sees it wherever the page is scrolled and whatever is open. No screen draws the outcome inline; only a
 * validation error tied to a form field stays beside that field. Screens that run actions with no row to attach an
 * outcome to (refresh rates, import, sync, delete) share this instead of each keeping a message and a flag.
 */
import { useToastStore } from '@/stores/toast'
import { UiTone } from '@/utils/ui/tone'

/** What {@link useOutcomeToast} hands back. */
export interface OutcomeToast {
  /** Toasts a success, or a refusal in the danger tone. */
  show: (text: string, failed: boolean) => void
}

/**
 * Creates the outcome reporter for one screen.
 *
 * @returns The action that toasts an outcome.
 */
export const useOutcomeToast = (): OutcomeToast => {
  /** The toasts an outcome is shown in. */
  const toasts = useToastStore()

  /**
   * Toasts an outcome.
   *
   * @param text - The finished sentence.
   * @param failed - Whether it reports a failure.
   */
  const show = (text: string, failed: boolean): void => {
    toasts.push(text, failed ? UiTone.Danger : UiTone.Success)
  }

  return { show }
}
