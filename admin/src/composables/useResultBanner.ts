/**
 * The outcome line of a screen: how the last action went.
 *
 * A success is a toast (`stores/toast.ts`): it confirms and dismisses itself, and takes no room in the page. A refusal
 * stays inline, in the banner this composable keeps, because the reader has to act on it: it hides after a few seconds, as long as a toast stays
 * or at once through `clear`. Screens that run actions with no row to attach an outcome to (refresh rates, import,
 * sync, delete) share this instead of each keeping a message, a flag and a timer.
 */
import { onBeforeUnmount, ref } from 'vue'
import type { Ref } from 'vue'
import { TOAST_MS, useToastStore } from '@/stores/toast'

/** What {@link useResultBanner} hands back. */
export interface ResultBanner {
  /** The refusal on show, or null when the banner is hidden. A success never appears here, it is a toast. */
  message: Ref<string | null>
  /** Whether the sentence on show reports a failure; false while the banner is hidden. */
  isError: Ref<boolean>
  /** Toasts a success, or shows a refusal for {@link TOAST_MS}, replacing any earlier one. */
  show: (text: string, failed: boolean) => void
  /** Hides the banner at once. */
  clear: () => void
}

/**
 * Creates the banner state for one screen.
 *
 * @returns The message, the error flag and the two actions; the pending timer is cancelled when the
 * screen unmounts.
 */
export const useResultBanner = (): ResultBanner => {
  /** The toasts a success is shown in. */
  const toasts = useToastStore()

  /** The refusal on show. */
  const message = ref<string | null>(null)

  /** Whether the sentence on show reports a failure. */
  const isError = ref(false)

  /** Handle of the pending auto-hide timer. */
  let timer: ReturnType<typeof setTimeout> | null = null

  /** Cancels the pending auto-hide timer, if any. */
  const cancelTimer = (): void => {
    if (timer !== null) {
      clearTimeout(timer)
      timer = null
    }
  }

  /**
   * Hides the banner at once.
   */
  const clear = (): void => {
    cancelTimer()
    message.value = null
    isError.value = false
  }

  /**
   * Toasts a success, or shows a refusal inline and schedules it to disappear. Either way an earlier refusal goes.
   *
   * @param text - The finished sentence.
   * @param failed - Whether it reports a failure.
   */
  const show = (text: string, failed: boolean): void => {
    clear()
    if (!failed) {
      toasts.push(text)
      return
    }
    message.value = text
    isError.value = true
    timer = setTimeout(clear, TOAST_MS)
  }

  onBeforeUnmount(cancelTimer)

  return { message, isError, show, clear }
}
