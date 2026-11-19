/**
 * The toasts on screen: short confirmations of what an action just did (saved, deleted, sent) that dismiss
 * themselves after a few seconds. A refusal or a warning is never a toast; it stays inline beside the thing that
 * failed, where the reader can act on it.
 *
 * `UiToastHost` draws the list; any screen reaches it through `useResultBanner().show(text, false)`.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { UiTone } from '@/utils/ui/tone'

/** How long a toast stays before it dismisses itself, in milliseconds. */
export const TOAST_MS = 5000

/** One toast on screen. */
export interface Toast {
  /** Identifier, unique for the life of the app; the key of the toast in the host. */
  id: number
  /** The finished sentence to show. */
  message: string
  /** Colour of the toast. */
  tone: UiTone
}

/** The toasts on screen and the actions that add and remove them. */
export const useToastStore = defineStore('toast', () => {
  /** The toasts on screen, oldest first. */
  const toasts = ref<Toast[]>([])

  /** The pending auto-dismiss timer of each toast, by id. */
  const timers = new Map<number, ReturnType<typeof setTimeout>>()

  /** Identifier handed to the next toast. */
  let nextId = 1

  /**
   * Removes a toast at once and cancels its timer.
   *
   * @param id - Identifier of the toast; an id that is not on screen is ignored.
   */
  const dismiss = (id: number): void => {
    const timer = timers.get(id)
    if (timer !== undefined) {
      clearTimeout(timer)
      timers.delete(id)
    }
    toasts.value = toasts.value.filter((toast) => toast.id !== id)
  }

  /**
   * Shows a toast that dismisses itself after {@link TOAST_MS}.
   *
   * @param message - The finished sentence.
   * @param tone - Colour of the toast; success by default.
   * @returns The identifier of the new toast.
   */
  const push = (message: string, tone: UiTone = UiTone.Success): number => {
    const id = nextId
    nextId += 1
    toasts.value = [...toasts.value, { id, message, tone }]
    timers.set(id, setTimeout(() => dismiss(id), TOAST_MS))
    return id
  }

  /** Removes every toast and cancels every pending timer. */
  const clear = (): void => {
    for (const timer of timers.values()) {
      clearTimeout(timer)
    }
    timers.clear()
    toasts.value = []
  }

  return { toasts, push, dismiss, clear }
})
