<script setup lang="ts">
/**
 * Kebab-menu (⋮) for table rows with two or more actions.
 *
 * A single icon button opens a dropdown list of named actions. The menu closes on Escape, on a
 * click outside the component, and after any action is activated. Destructive actions are styled
 * in red. Disabled actions are shown faded and cannot be selected.
 *
 * Keyboard behaviour follows the WAI-ARIA APG "Menu Button" pattern:
 * - Enter / click on the trigger opens the menu and moves focus to the first enabled item.
 * - ArrowDown / ArrowUp cycle focus through the enabled items.
 * - Escape closes the menu and returns focus to the trigger button.
 *
 * The panel is teleported to `<body>` and placed with `position: fixed` next to the trigger, so a
 * table's horizontal scroller (or any `overflow` ancestor) can neither clip it nor grow a scrollbar
 * when a menu opens under the last row. It closes on scroll and resize instead of following.
 */
import { nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiPanelTransition from '@/components/ui/UiPanelTransition.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import { Icon } from '@/lib/icons'

/** One item in the actions menu. */
export interface UiRowAction {
  /** Visible label and accessible name of the action. */
  label: string
  /** Called when the user activates the action. */
  onSelect: () => void
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
}>()

const { t } = useI18n()

/** Whether the dropdown is open. */
const open = ref(false)

/** Root element ref for outside-click detection. */
const root = ref<HTMLElement | null>(null)

/** The trigger button — focus returns here after the menu closes. */
const triggerRef = ref<HTMLButtonElement | null>(null)

/** The dropdown panel — used for focus management. */
const menuRef = ref<HTMLElement | null>(null)

/**
 * Whether to render the menu above the trigger instead of below it.
 * Set just before the menu opens so the first render is already in the right position.
 */
const flipUp = ref(false)

/** Toggles the dropdown. */
const toggle = (): void => {
  if (!open.value && triggerRef.value) {
    // Decide placement before the menu is inserted into the DOM.
    const rect = triggerRef.value.getBoundingClientRect()
    flipUp.value = window.innerHeight - rect.bottom < 160
  }
  open.value = !open.value
}

/** Gap between the trigger and the panel, in pixels. */
const PANEL_GAP = 4

/** Places the fixed panel against the trigger: right edges aligned, below it or above when flipped. */
const placeMenu = (): void => {
  const trigger = triggerRef.value
  const menu = menuRef.value
  if (!trigger || !menu) { return }
  const rect = trigger.getBoundingClientRect()
  menu.style.setProperty('--ra-right', `${window.innerWidth - rect.right}px`)
  menu.style.setProperty('--ra-top', `${rect.bottom + PANEL_GAP}px`)
  menu.style.setProperty('--ra-bottom', `${window.innerHeight - rect.top + PANEL_GAP}px`)
}

/** Closes the menu when the page scrolls or resizes, since the fixed panel would drift from its trigger. */
const closeOnViewportChange = (): void => {
  open.value = false
}

/**
 * Activates an action and closes the menu.
 *
 * @param action - The action the user picked.
 */
const select = (action: UiRowAction): void => {
  if (action.disabled) { return }
  open.value = false
  action.onSelect()
  triggerRef.value?.focus()
}

/**
 * Closes the menu when the pointer goes down outside the component.
 *
 * @param e - The mouse event.
 */
const onOutsideClick = (e: MouseEvent): void => {
  const target = e.target as Node
  if (root.value && !root.value.contains(target) && !menuRef.value?.contains(target)) {
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
    const items = Array.from(
      menuRef.value?.querySelectorAll<HTMLButtonElement>('button:not([disabled])') ?? [],
    )
    if (!items.length) { return }
    const idx = items.indexOf(document.activeElement as HTMLButtonElement)
    const next =
      e.key === 'ArrowDown'
        ? items[(idx + 1) % items.length]
        : items[(idx - 1 + items.length) % items.length]
    next?.focus()
  }
}

// Move focus to the first enabled menu item as soon as the panel is in the DOM.
watch(open, async (val) => {
  if (!val) { return }
  await nextTick()
  placeMenu()
  const first = menuRef.value?.querySelector<HTMLButtonElement>('button:not([disabled])')
  first?.focus()
})

onMounted(() => {
  document.addEventListener('mousedown', onOutsideClick)
  document.addEventListener('keydown', onKeydown)
  window.addEventListener('scroll', closeOnViewportChange, true)
  window.addEventListener('resize', closeOnViewportChange)
})

onUnmounted(() => {
  document.removeEventListener('mousedown', onOutsideClick)
  document.removeEventListener('keydown', onKeydown)
  window.removeEventListener('scroll', closeOnViewportChange, true)
  window.removeEventListener('resize', closeOnViewportChange)
})
</script>

<template>
  <div ref="root" class="relative inline-block" data-testid="row-actions">
    <!-- Trigger -->
    <button
      ref="triggerRef"
      type="button"
      :aria-label="t('shared.actions')"
      aria-haspopup="menu"
      :aria-expanded="open"
      class="inline-flex items-center justify-center rounded-lg p-1 text-text-muted transition-colors hover:bg-surface-raised hover:text-text-primary focus-visible:outline-2 focus-visible:outline-primary-500"
      data-testid="row-actions-trigger"
      @click="toggle"
    >
      <UiIcon :markup="Icon.MoreVertical" size="sm" />
    </button>

    <!-- Dropdown -->
    <Teleport to="body">
      <UiPanelTransition>
        <div
          v-if="open"
          ref="menuRef"
          role="menu"
          :class="[
            'fixed right-[var(--ra-right)] z-50 min-w-[10rem] rounded-[9px] border border-border-strong bg-surface-card p-1 shadow-xl',
            flipUp ? 'bottom-[var(--ra-bottom)]' : 'top-[var(--ra-top)]',
          ]"
          data-testid="row-actions-menu"
        >
          <button
            v-for="action in props.actions"
            :key="action.testid ?? action.label"
            type="button"
            role="menuitem"
            :data-testid="action.testid"
            :disabled="action.disabled"
            class="flex w-full cursor-pointer items-center rounded-md px-3 py-1.5 text-left text-sm transition-colors hover:bg-surface-hover focus-visible:outline-none focus-visible:bg-surface-hover disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:bg-transparent"
            :class="action.danger ? 'text-danger-600 dark:text-danger-400' : 'text-text-primary'"
            @click="select(action)"
          >
            {{ action.label }}
          </button>
        </div>
      </UiPanelTransition>
    </Teleport>
  </div>
</template>
