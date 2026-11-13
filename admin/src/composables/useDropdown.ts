/**
 * The open/close shell every floating list of the kit shares: the open flag, closing when the
 * pointer goes down outside the component, and closing on Escape with the focus handed back to the
 * trigger. `UiSelect`, `UiMenu`, `UiFieldSelector` and the date pickers all sit on it, so the behaviour is written
 * once. The panel is drawn in the page body (a teleport, see `useFloatingPlace`), so it is not inside the root in
 * the DOM: a pointer-down on it belongs to the component all the same, which is what `panel` and `isInside` tell.
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
  /** The floating panel itself, teleported out of the root. Bind with `ref="panel"` on the panel element. */
  panel: Ref<HTMLElement | null>
  /** The control that opens the panel; it gets the focus back when Escape closes it. Bind with `ref="trigger"`. */
  trigger: Ref<HTMLElement | null>
  /**
   * Whether a node belongs to the component: inside the root (the trigger) or inside the teleported panel.
   *
   * @param node - The node to test; anything that is not a node (a window, null) is outside.
   */
  isInside: (node: EventTarget | null) => boolean
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

  /** The floating panel, teleported out of the root. */
  const panel = ref<HTMLElement | null>(null)

  /** The control that opens the panel. */
  const trigger = ref<HTMLElement | null>(null)

  /**
   * Whether a node belongs to the component: inside the root or inside the teleported panel.
   *
   * @param node - The node to test.
   * @returns True when the node is in the trigger area or the panel.
   */
  const isInside = (node: EventTarget | null): boolean => {
    return node instanceof Node && ((root.value?.contains(node) ?? false) || (panel.value?.contains(node) ?? false))
  }

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
   * Closes the panel when the pointer goes down outside the component (its root and its panel).
   *
   * @param event - The mouse event.
   */
  const onOutsideDown = (event: MouseEvent): void => {
    if (event.target instanceof Node && !isInside(event.target)) {
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

  return { open, root, trigger, panel, isInside, show, hide, toggle, onEscape }
}
