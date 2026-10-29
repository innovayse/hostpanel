<script setup lang="ts">
/**
 * Kebab-menu (⋮) for table rows with two or more actions.
 *
 * A single icon button opens a dropdown list of named actions. The menu closes on Escape, on a
 * click outside the component, and after any action is activated. Destructive actions are styled
 * in red. WAI-ARIA attributes follow the APG "Menu Button" pattern.
 */
import { onMounted, onUnmounted, ref } from 'vue'
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

/** Toggles the dropdown. */
const toggle = (): void => {
  open.value = !open.value
}

/**
 * Activates an action and closes the menu.
 *
 * @param action - The action the user picked.
 */
const select = (action: UiRowAction): void => {
  open.value = false
  action.onSelect()
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
 * Closes the menu on Escape.
 *
 * @param e - The keyboard event.
 */
const onKeydown = (e: KeyboardEvent): void => {
  if (e.key === 'Escape') {
    open.value = false
  }
}

onMounted(() => {
  document.addEventListener('mousedown', onOutsideClick)
  document.addEventListener('keydown', onKeydown)
})

onUnmounted(() => {
  document.removeEventListener('mousedown', onOutsideClick)
  document.removeEventListener('keydown', onKeydown)
})
</script>

<template>
  <div ref="root" class="relative inline-block" data-testid="row-actions">
    <!-- Trigger -->
    <button
      type="button"
      :aria-label="t('shared.actions')"
      :aria-haspopup="true"
      :aria-expanded="open"
      class="inline-flex items-center justify-center rounded-lg p-1 text-text-muted transition-colors hover:bg-surface-raised hover:text-text-primary focus-visible:outline-2 focus-visible:outline-primary-500"
      data-testid="row-actions-trigger"
      @click="toggle"
    >
      <UiIcon :markup="Icon.MoreVertical" size="sm" />
    </button>

    <!-- Dropdown -->
    <div
      v-if="open"
      role="menu"
      class="absolute right-0 z-20 mt-1 min-w-[10rem] rounded-[9px] border border-border bg-surface-base py-1 shadow-lg"
      data-testid="row-actions-menu"
    >
      <button
        v-for="action in props.actions"
        :key="action.label"
        type="button"
        role="menuitem"
        :data-testid="action.testid"
        class="flex w-full items-center px-3 py-1.5 text-left text-sm transition-colors hover:bg-surface-raised focus-visible:outline-none focus-visible:bg-surface-raised"
        :class="action.danger ? 'text-danger-600 dark:text-danger-400' : 'text-text-primary'"
        @click="select(action)"
      >
        {{ action.label }}
      </button>
    </div>
  </div>
</template>
