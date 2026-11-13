/**
 * The open/close shell every floating list of the kit shares: the open flag, closing when the
 * pointer goes down outside the component, and closing on Escape with the focus handed back to the
 * trigger. `UiSelect` and `UiFieldSelector` both sit on it, so the behaviour is written once.
 */
import { onMounted, onUnmounted, ref } from 'vue'
import type { Ref } from 'vue'
import { Key } from '@/utils/ui/keys'

/** What {@link useDropdown} hands back. */
export interface Dropdown {
  /** Whether the floating panel is open. */
  open: Ref<boolean>
  /** Root element of the component: a pointer-down outside it closes the panel. Bind with `ref="root"`. */
  root: Ref<HTMLElement | null>
  /** The control that opens the panel; it gets the focus back when Escape closes it. Bind with `ref="trigger"`. */
  trigger: Ref<HTMLElement | null>
  /** Opens the panel. */
  show: () => void
  /** Closes the panel, optionally handing the focus back to the trigger. */
  hide: (restoreFocus?: boolean) => void
  /** Flips the panel. */
  toggle: () => void
  /**
   * Closes the panel on Escape and swallows the key, so a dialog around the component stays open.
   *
   * @param event - The key event; any other key is ignored.
   */
  onEscape: (event: KeyboardEvent) => void
}

/**
 * Creates the open state of one floating panel and listens for pointer-downs outside it for as long
 * as the component lives.
 *
 * @returns The state, the two element refs to bind in the template and the actions.
 */
export const useDropdown = (): Dropdown => {
  /** Whether the floating panel is open. */
  const open = ref(false)

  /** Root element, used for outside-click detection. */
  const root = ref<HTMLElement | null>(null)

  /** The control that opens the panel. */
  const trigger = ref<HTMLElement | null>(null)

  /** Opens the panel. */
  const show = (): void => {
    open.value = true
  }

  /**
   * Closes the panel.
   *
   * @param restoreFocus - Hand the focus back to the trigger, for a close the reader asked for by key or pick.
   */
  const hide = (restoreFocus = false): void => {
    open.value = false
    if (restoreFocus) {
      trigger.value?.focus()
    }
  }

  /** Flips the panel. */
  const toggle = (): void => {
    open.value = !open.value
  }

  /**
   * Closes the panel when the pointer goes down outside the component.
   *
   * @param event - The mouse event.
   */
  const onOutsideDown = (event: MouseEvent): void => {
    if (root.value && event.target instanceof Node && !root.value.contains(event.target)) {
      open.value = false
    }
  }

  /**
   * Closes the panel on Escape.
   *
   * @param event - The key event.
   */
  const onEscape = (event: KeyboardEvent): void => {
    if (event.key === Key.Escape && open.value) {
      event.preventDefault()
      event.stopPropagation()
      hide(true)
    }
  }

  onMounted(() => document.addEventListener('mousedown', onOutsideDown))
  onUnmounted(() => document.removeEventListener('mousedown', onOutsideDown))

  return { open, root, trigger, show, hide, toggle, onEscape }
}
