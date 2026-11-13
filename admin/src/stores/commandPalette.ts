/**
 * The command palette store: whether the palette is open and which pages the viewer opened lately.
 *
 * The top bar calls {@link useCommandPaletteStore} `open` or `toggle`; the palette itself
 * (`components/layout/CommandPalette.vue`) listens for Ctrl K / Cmd K and calls the same actions.
 */
import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { readRecent, rememberRecent } from '@/lib/commandPalette'
import { useAuthStore } from '@/stores/auth'

/**
 * Pinia store holding the palette's open state and the signed-in viewer's recent pages.
 *
 * Recent pages are kept per viewer in `localStorage`, under a key made from a hash of the account's
 * email (never the address itself), and read again each time the palette opens, so a different
 * account on the same browser never sees another's history. Signing out clears them.
 */
export const useCommandPaletteStore = defineStore('commandPalette', () => {
  /** Auth store, for the viewer the recent pages belong to. */
  const auth = useAuthStore()

  /** Whether the palette is shown. */
  const isOpen = ref(false)

  /** Paths the viewer opened lately, newest first. */
  const recent = ref<string[]>([])

  /** The viewer's email, or an empty string while nobody is signed in. */
  const viewer = computed<string>(() => auth.user?.email ?? '')

  /** Opens the palette, reading the viewer's recent pages first. */
  const open = (): void => {
    recent.value = readRecent(viewer.value)
    isOpen.value = true
  }

  /** Closes the palette. */
  const close = (): void => {
    isOpen.value = false
  }

  /** Opens the palette when it is closed and closes it when it is open. */
  const toggle = (): void => {
    if (isOpen.value) {
      close()
    } else {
      open()
    }
  }

  /**
   * Records a page as just opened from the palette.
   *
   * @param path - The route path that was opened.
   */
  const remember = (path: string): void => {
    recent.value = rememberRecent(viewer.value, recent.value, path)
  }

  return { isOpen, recent, open, close, toggle, remember }
})
