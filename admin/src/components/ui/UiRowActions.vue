<script setup lang="ts">
/**
 * The one actions control of a table row.
 *
 * One action is shown as that single labelled button. Two or more sit behind one kebab (⋮) trigger with an
 * accessible label that opens a dropdown list of named actions. Destructive actions move to the end of the
 * list in the danger tone; each still asks for confirmation in its own `onSelect`. The menu closes on Escape,
 * on a click outside the component, and after any action is activated. Disabled actions are shown faded and
 * cannot be selected. No actions render nothing. An action with a `to` route is a real link (an anchor via RouterLink), so
 * middle click, Ctrl/Cmd click and "open in new tab" work and assistive technology announces a link.
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
import { RouterLink, type RouteLocationRaw } from 'vue-router'
import UiIcon from '@/components/ui/UiIcon.vue'
import { Icon } from '@/lib/icons'

/** One item in the actions menu. */
export interface UiRowAction {
  /** Visible label and accessible name of the action. */
  label: string
  /** Called when the user activates the action; optional for a link action, whose navigation is the `to` route. */
  onSelect?: () => void
  /** Route the action opens. When set (and the action is enabled) the item renders as a RouterLink anchor. */
  to?: RouteLocationRaw
  /** Styles the item as a destructive (red) action. */
  danger?: boolean
  /** Prevents the item from being selected; it is shown faded. */
  disabled?: boolean
  /** `data-testid` forwarded to the menu item button. */
  testid?: string
}

/** Props for UiRowActions. */
const props = defineProps<{
  /** The actions to show in the menu. */
  actions: UiRowAction[]
  /** Per-row unique `data-testid` of the kebab trigger; `row-actions-trigger` when not given. */
  testid?: string
}>()

const { t } = useI18n()

/** The actions in display order: the destructive ones last, each group keeping the order it was given in. */
const ordered = computed<UiRowAction[]>(() => [
  ...props.actions.filter((action) => !action.danger),
  ...props.actions.filter((action) => action.danger),
])

/** The only action of the row, shown as a plain button instead of a menu; null when there are none or several. */
const single = computed<UiRowAction | null>(() => (ordered.value.length === 1 ? ordered.value[0] ?? null : null))

/** The `data-testid` of the kebab trigger: the caller's per-row id, or the shared default. */
const triggerTestid = computed<string>(() => props.testid ?? 'row-actions-trigger')

/** Selector of the focusable, enabled menu items, buttons and link anchors alike. */
const ITEM_SELECTOR = '[role="menuitem"]:not([disabled])'

/**
 * Whether an action renders as a link: it has a route and is not disabled (a disabled link cannot be inert).
 *
 * @param action - The action to check.
 * @returns True when the action is an enabled link.
 */
const isLink = (action: UiRowAction): action is UiRowAction & { to: RouteLocationRaw } => {
  return action.to !== undefined && !action.disabled
}

/** Whether the dropdown is open. */
const open = ref(false)

/** Root element ref for outside-click detection. */
const root = ref<HTMLElement | null>(null)

/** The trigger button — focus returns here after the menu closes. */
const triggerRef = ref<HTMLButtonElement | null>(null)

/** The dropdown panel — used for focus management. */
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
const select = (action: UiRowAction): void => {
  if (action.disabled) { return }
  open.value = false
  action.onSelect?.()
  if (!isLink(action)) {
    triggerRef.value?.focus()
  }
}

/**
 * Activates the single action of a row; there is no menu to close.
 *
 * @param action - The row's only action.
 */
const selectSingle = (action: UiRowAction): void => {
  if (action.disabled) { return }
  action.onSelect?.()
}

/**
 * Closes the menu when the pointer goes down outside the component.
 *
 * @param e - The mouse event.
 */
const onOutsideClick = (e: MouseEvent): void => {
  if (root.value && !root.value.contains(e.target as Node)) {
    open.value = false
  }
}

/**
 * Closes the menu when focus moves to something outside the component (Tab out of the menu), leaving focus where
 * the user sent it. Focus moving between the trigger and the items stays inside the root and keeps the menu open.
 *
 * @param e - The focusout event; `relatedTarget` is the element receiving focus, or null when focus leaves the page.
 */
const onFocusOut = (e: FocusEvent): void => {
  if (!open.value) { return }
  const next = e.relatedTarget as Node | null
  if (next && root.value && !root.value.contains(next)) {
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
  <div v-if="single" class="inline-block" data-testid="row-actions">
    <!-- One action: the action itself, no menu; a link when it has a route -->
    <RouterLink
      v-if="isLink(single)"
      :to="single.to"
      :data-testid="single.testid ?? 'row-actions-single'"
      class="inline-flex items-center justify-center rounded-lg px-2 py-1 text-sm font-medium transition-colors hover:bg-surface-raised focus-visible:outline-2 focus-visible:outline-primary-500"
      :class="single.danger ? 'text-danger-600 dark:text-danger-400' : 'text-text-primary'"
      @click="selectSingle(single)"
    >
      {{ single.label }}
    </RouterLink>
    <button
      v-else
      type="button"
      :data-testid="single.testid ?? 'row-actions-single'"
      :disabled="single.disabled"
      class="inline-flex items-center justify-center rounded-lg px-2 py-1 text-sm font-medium transition-colors hover:bg-surface-raised focus-visible:outline-2 focus-visible:outline-primary-500 disabled:cursor-not-allowed disabled:opacity-40"
      :class="single.danger ? 'text-danger-600 dark:text-danger-400' : 'text-text-primary'"
      @click="selectSingle(single)"
    >
      {{ single.label }}
    </button>
  </div>
  <div v-else-if="ordered.length > 1" ref="root" class="relative inline-block" data-testid="row-actions" @focusout="onFocusOut">
    <!-- Trigger -->
    <button
      ref="triggerRef"
      type="button"
      :aria-label="t('shared.actions')"
      aria-haspopup="menu"
      :aria-expanded="open"
      class="inline-flex items-center justify-center rounded-lg p-1 text-text-muted transition-colors hover:bg-surface-raised hover:text-text-primary focus-visible:outline-2 focus-visible:outline-primary-500"
      :data-testid="triggerTestid"
      @click="toggle"
    >
      <UiIcon :markup="Icon.MoreVertical" size="sm" />
    </button>

    <!-- Dropdown -->
    <UiTransition :kind="UiTransitionKind.Dropdown">
      <div
        v-if="open"
        ref="menuRef"
        role="menu"
        class="fixed z-20 min-w-[10rem] rounded-[9px] border border-border bg-surface-base py-1 shadow-lg right-[var(--ra-right)] top-[var(--ra-top,auto)] bottom-[var(--ra-bottom,auto)]"
        data-testid="row-actions-menu"
      >
        <template v-for="action in ordered" :key="action.testid ?? action.label">
          <RouterLink
            v-if="isLink(action)"
            :to="action.to"
            role="menuitem"
            :data-testid="action.testid"
            class="flex w-full items-center px-3 py-1.5 text-left text-sm transition-colors hover:bg-surface-raised focus-visible:outline-none focus-visible:bg-surface-raised"
            :class="action.danger ? 'text-danger-600 dark:text-danger-400' : 'text-text-primary'"
            @click="select(action)"
          >
            {{ action.label }}
          </RouterLink>
          <button
            v-else
            type="button"
            role="menuitem"
            :data-testid="action.testid"
            :disabled="action.disabled"
            class="flex w-full items-center px-3 py-1.5 text-left text-sm transition-colors hover:bg-surface-raised focus-visible:outline-none focus-visible:bg-surface-raised disabled:cursor-not-allowed disabled:opacity-40"
            :class="action.danger ? 'text-danger-600 dark:text-danger-400' : 'text-text-primary'"
            @click="select(action)"
          >
            {{ action.label }}
          </button>
        </template>
      </div>
    </UiTransition>
  </div>
</template>
