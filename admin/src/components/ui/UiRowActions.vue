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
 */
import { nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import UiTransition from '@/components/ui/UiTransition.vue'
import { UiTransitionKind } from '@/utils/ui/transition'
import { useI18n } from 'vue-i18n'
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

/** Gap between the trigger and the menu, in pixels. */
const MENU_GAP_PX = 4

/**
 * Where the menu sits on the screen. The menu is `fixed`, not `absolute`, so a table that scrolls inside its
 * card (`overflow-x-auto`) cannot clip it or grow a scrollbar for it; its place is worked out from the trigger
 * just before it opens, so the first render is already right. It opens above the trigger when there is little
 * room below.
 */
const menuPlace = ref<{ right: string; top?: string; bottom?: string }>({ right: '0px' })

/** Toggles the dropdown. */
const toggle = (): void => {
  if (!open.value && triggerRef.value) {
    const rect = triggerRef.value.getBoundingClientRect()
    const right = `${window.innerWidth - rect.right}px`
    menuPlace.value = window.innerHeight - rect.bottom < 160
      ? { right, bottom: `${window.innerHeight - rect.top + MENU_GAP_PX}px` }
      : { right, top: `${rect.bottom + MENU_GAP_PX}px` }
  }
  open.value = !open.value
}

/**
 * Closes the menu when the page or a scrolling table moves under it, because a fixed menu would stay behind.
 *
 * @returns Nothing; the menu is closed.
 */
const closeOnMove = (): void => {
  if (open.value) {
    open.value = false
  }
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
  if (root.value && !root.value.contains(e.target as Node)) {
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
  const first = menuRef.value?.querySelector<HTMLButtonElement>('button:not([disabled])')
  first?.focus()
})

onMounted(() => {
  document.addEventListener('mousedown', onOutsideClick)
  document.addEventListener('keydown', onKeydown)
  window.addEventListener('scroll', closeOnMove, { capture: true, passive: true })
  window.addEventListener('resize', closeOnMove)
})

onUnmounted(() => {
  document.removeEventListener('mousedown', onOutsideClick)
  document.removeEventListener('keydown', onKeydown)
  window.removeEventListener('scroll', closeOnMove, true)
  window.removeEventListener('resize', closeOnMove)
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
    <UiTransition :kind="UiTransitionKind.Dropdown">
      <div
        v-if="open"
        ref="menuRef"
        role="menu"
        class="fixed z-20 min-w-[10rem] rounded-[9px] border border-border bg-surface-base py-1 shadow-lg"
        :style="menuPlace"
        data-testid="row-actions-menu"
      >
        <button
          v-for="action in props.actions"
          :key="action.testid ?? action.label"
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
      </div>
    </UiTransition>
  </div>
</template>
