<script setup lang="ts">
/**
 * Dropdown menu of named actions behind one trigger button.
 *
 * The trigger shows the `default` slot (a kebab ⋮ icon when the slot is empty) and carries an accessible label
 * (`ariaLabel`, "Actions" by default). Destructive actions move to the end of the list in the danger tone; each still
 * asks for confirmation in its own `onSelect`. The menu closes on Escape, on a click outside the component, and after
 * any action is activated. The list is drawn in the page body (a teleport), because a frosted card around it would
 * otherwise become the containing block of a fixed box. Disabled actions are shown faded and cannot be selected. An
 * action with a `to` route is a real link (an anchor via RouterLink), so middle click, Ctrl/Cmd click and "open in new
 * tab" work and assistive technology announces a link. No actions render nothing.
 *
 * An action with `checked` set is one choice of a group: its row is a `menuitemradio` with `aria-checked`, and a check
 * mark is drawn while it is the current choice.
 *
 * Keyboard behaviour follows the WAI-ARIA APG "Menu Button" pattern:
 * - Enter / click on the trigger opens the menu and moves focus to the first enabled item.
 * - ArrowDown / ArrowUp cycle focus through the enabled items.
 * - Escape closes the menu and returns focus to the trigger button.
 * - Tab moving focus out of the component closes the menu without taking focus back.
 */
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import UiTransition from '@/components/ui/UiTransition.vue'
import { UiTransitionKind } from '@/utils/ui/transition'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'
import UiIcon from '@/components/ui/UiIcon.vue'
import { Icon } from '@/lib/icons'
import { elementAttrs, isLink, itemRole, toneClasses } from '@/utils/ui/menu'
import type { UiMenuAction } from '@/utils/ui/menu'

/** Props for UiMenu. */
const props = defineProps<{
  /** The actions to show in the menu. */
  actions: UiMenuAction[]
  /** Accessible label of the trigger; the shared "Actions" text when not given. */
  ariaLabel?: string
  /** `data-testid` of the trigger; `ui-menu-trigger` when not given. */
  testid?: string
  /** `data-testid` of the open menu panel; `ui-menu-panel` when not given. */
  menuTestid?: string
}>()

const { t } = useI18n()

/** The actions in display order: the destructive ones last, each group keeping the order it was given in. */
const ordered = computed<UiMenuAction[]>(() => [
  ...props.actions.filter((action) => !action.danger),
  ...props.actions.filter((action) => action.danger),
])

/** The accessible label of the trigger: the caller's, or the shared "Actions" text. */
const triggerLabel = computed<string>(() => props.ariaLabel ?? t('shared.actions'))

/** The `data-testid` of the trigger: the caller's id, or the shared default. */
const triggerTestid = computed<string>(() => props.testid ?? 'ui-menu-trigger')

/** The `data-testid` of the open panel: the caller's id, or the shared default. */
const panelTestid = computed<string>(() => props.menuTestid ?? 'ui-menu-panel')

/** Look of one row of the open menu. */
const MENU_ITEM_CLASSES = 'flex w-full items-center gap-2 px-3 py-2 text-left text-sm transition-colors hover:bg-surface-hover focus-visible:outline-none focus-visible:bg-surface-hover disabled:cursor-not-allowed disabled:opacity-40'

/** Selector of the focusable, enabled menu items, buttons and link anchors alike. */
const ITEM_SELECTOR = '[role^="menuitem"]:not([disabled])'

/** Whether the dropdown is open. */
const open = ref(false)

/** Root element ref for outside-click detection. */
const root = ref<HTMLElement | null>(null)

/** The trigger button: focus returns here after the menu closes. */
const triggerRef = ref<HTMLButtonElement | null>(null)

/** The dropdown panel: used for focus management. */
const menuRef = ref<HTMLElement | null>(null)

/** Gap between the trigger and the menu, in pixels. */
const MENU_GAP_PX = 4

/** Free room below the trigger, in pixels, under which the menu opens above it. */
const MENU_MIN_ROOM_PX = 160

/**
 * Where the menu sits on the screen, in pixels from the viewport edges. The menu is `fixed`, not `absolute`, so a
 * table that scrolls inside its card (`overflow-x-auto`) cannot clip it or grow a scrollbar for it; its place is
 * worked out from the trigger just before it opens. It opens above the trigger when there is little room below.
 * It reaches the element as CSS variables (see `applyPlace`), never as a style attribute.
 */
const menuPlace = ref<{ right: number; top?: number; bottom?: number }>({ right: 0 })

/**
 * Writes the menu place into CSS variables on the menu element, the way UiDateRangePicker places its popup.
 * The unused vertical edge is removed so the class fallback (`auto`) applies.
 */
const applyPlace = (): void => {
  const el = menuRef.value
  if (!el) { return }
  const place = menuPlace.value
  el.style.setProperty('--ra-right', `${place.right}px`)
  if (place.top !== undefined) {
    el.style.setProperty('--ra-top', `${place.top}px`)
  } else {
    el.style.removeProperty('--ra-top')
  }
  if (place.bottom !== undefined) {
    el.style.setProperty('--ra-bottom', `${place.bottom}px`)
  } else {
    el.style.removeProperty('--ra-bottom')
  }
}

/** Closes the menu when the page or a scrolling table moves under it, because a fixed menu would stay behind. */
const closeOnMove = (): void => {
  open.value = false
}

/** Starts listening for scroll and resize; called when the menu opens. */
const listenForMove = (): void => {
  window.addEventListener('scroll', closeOnMove, { capture: true, passive: true })
  window.addEventListener('resize', closeOnMove)
}

/** Stops listening for scroll and resize; called when the menu closes and on unmount. */
const stopListeningForMove = (): void => {
  window.removeEventListener('scroll', closeOnMove, true)
  window.removeEventListener('resize', closeOnMove)
}

/** Toggles the dropdown. */
const toggle = (): void => {
  if (!open.value && triggerRef.value) {
    const rect = triggerRef.value.getBoundingClientRect()
    // `clientWidth` and `clientHeight` are the viewport without the page scrollbars, which a `fixed` box's
    // `right` and `bottom` are measured from (`innerWidth` and `innerHeight` include them).
    const doc = document.documentElement
    const right = doc.clientWidth - rect.right
    menuPlace.value = doc.clientHeight - rect.bottom < MENU_MIN_ROOM_PX
      ? { right, bottom: doc.clientHeight - rect.top + MENU_GAP_PX }
      : { right, top: rect.bottom + MENU_GAP_PX }
  }
  open.value = !open.value
}

/**
 * Activates an action and closes the menu.
 *
 * @param action - The action the user picked.
 */
const select = (action: UiMenuAction): void => {
  if (action.disabled) { return }
  open.value = false
  action.onSelect?.()
  if (!isLink(action)) {
    triggerRef.value?.focus()
  }
}

/**
 * Whether a node belongs to the component: its root (the trigger) or the menu, which is teleported out of the root.
 *
 * @param node - The node to test.
 * @returns True when the node is inside the trigger area or the menu.
 */
const isInside = (node: Node): boolean => {
  return (root.value?.contains(node) ?? false) || (menuRef.value?.contains(node) ?? false)
}

/**
 * Closes the menu when the pointer goes down outside the component.
 *
 * @param e - The mouse event.
 */
const onOutsideClick = (e: MouseEvent): void => {
  if (root.value && !isInside(e.target as Node)) {
    open.value = false
  }
}

/**
 * Closes the menu when focus moves to something outside the component (Tab out of the menu), leaving focus where
 * the user sent it. Focus moving between the trigger and the items stays inside the component and keeps the menu open.
 *
 * @param e - The focusout event; `relatedTarget` is the element receiving focus, or null when focus leaves the page.
 */
const onFocusOut = (e: FocusEvent): void => {
  if (!open.value) { return }
  const next = e.relatedTarget as Node | null
  if (next && root.value && !isInside(next)) {
    open.value = false
  }
}

/**
 * Handles keyboard navigation inside the open menu.
 * - Escape: close and return focus to trigger.
 * - ArrowDown / ArrowUp: move focus between enabled items.
 *
 * @param e - The keyboard event.
 */
const onKeydown = (e: KeyboardEvent): void => {
  if (e.key === 'Escape' && open.value) {
    open.value = false
    triggerRef.value?.focus()
    return
  }

  if (!open.value) { return }

  if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
    e.preventDefault()
    const items = Array.from(menuRef.value?.querySelectorAll<HTMLElement>(ITEM_SELECTOR) ?? [])
    if (!items.length) { return }
    const idx = items.indexOf(document.activeElement as HTMLElement)
    const next =
      e.key === 'ArrowDown'
        ? items[(idx + 1) % items.length]
        : items[(idx - 1 + items.length) % items.length]
    next?.focus()
  }
}

// The menu element exists after the render, so its place is written then, and the scroll and resize listeners
// live only while it is open. Focus moves to the first enabled item.
watch(open, async (val) => {
  if (!val) {
    stopListeningForMove()
    return
  }
  await nextTick()
  // Closed again before the render finished: attach nothing.
  if (!open.value) { return }
  applyPlace()
  listenForMove()
  const first = menuRef.value?.querySelector<HTMLElement>(ITEM_SELECTOR)
  first?.focus()
})

onMounted(() => {
  document.addEventListener('mousedown', onOutsideClick)
  document.addEventListener('keydown', onKeydown)
})

onUnmounted(() => {
  document.removeEventListener('mousedown', onOutsideClick)
  document.removeEventListener('keydown', onKeydown)
  stopListeningForMove()
})
</script>

<template>
  <div v-if="ordered.length > 0" ref="root" class="relative inline-block" @focusout="onFocusOut">
    <!-- Trigger: the slot content, a kebab by default -->
    <button
      ref="triggerRef"
      type="button"
      :aria-label="triggerLabel"
      aria-haspopup="menu"
      :aria-expanded="open"
      class="group inline-flex items-center justify-center rounded-control focus-visible:outline-2 focus-visible:outline-primary-500"
      :data-testid="triggerTestid"
      @click="toggle"
    >
      <slot>
        <span class="inline-flex size-8 items-center justify-center rounded-control text-text-muted transition-colors group-hover:bg-surface-hover group-hover:text-text-primary">
          <UiIcon :markup="Icon.MoreVertical" size="sm" />
        </span>
      </slot>
    </button>

    <!-- Dropdown: in the page body, so no card around the trigger can clip it or re-anchor its fixed place -->
    <Teleport to="body">
      <UiTransition :kind="UiTransitionKind.Dropdown">
        <div
          v-if="open"
          ref="menuRef"
          role="menu"
          class="glass-strong fixed z-50 min-w-[12rem] rounded-control p-1.5 shadow-popover right-[var(--ra-right)] top-[var(--ra-top,auto)] bottom-[var(--ra-bottom,auto)]"
          :data-testid="panelTestid"
          @focusout="onFocusOut"
        >
          <component
            :is="isLink(action) ? RouterLink : 'button'"
            v-for="action in ordered"
            :key="action.testid ?? action.label"
            v-bind="elementAttrs(action)"
            :role="itemRole(action)"
            :aria-checked="action.checked"
            :data-testid="action.testid"
            class="rounded-lg"
            :class="[MENU_ITEM_CLASSES, toneClasses(action)]"
            @click="select(action)"
          >
            <UiIcon v-if="action.icon" :markup="action.icon" size="sm" />
            {{ action.label }}
            <UiIcon v-if="action.checked" :markup="Icon.Check" size="sm" class="ml-auto" />
          </component>
        </div>
      </UiTransition>
    </Teleport>
  </div>
</template>
