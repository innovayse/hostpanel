/**
 * Helpers for the screen tests that check how an action ended: the outcome of an action is a toast, so a test reads the
 * toast store instead of looking for an inline message in the screen.
 */
import { useToastStore } from '@/stores/toast'
import type { UiTone } from '@/utils/ui/tone'

/**
 * The sentence of every toast on screen.
 *
 * @returns One sentence per toast, oldest first.
 */
export const toastMessages = (): string[] => useToastStore().toasts.map((toast) => toast.message)

/**
 * The toasts on screen as sentence and tone pairs.
 *
 * @returns One pair per toast, oldest first.
 */
export const toastsOnScreen = (): Array<[string, UiTone]> => useToastStore().toasts.map((toast) => [toast.message, toast.tone])
