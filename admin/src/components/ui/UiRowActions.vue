<script setup lang="ts">
/**
 * The one actions control of a table row.
 *
 * One action is shown as that single icon button (its label is the accessible name and the tooltip, or the
 * action's longer `hint` when it has one), so the actions column stays narrow. Two or more sit behind one "…"
 * (MoreHorizontal) trigger in a {@link UiMenu}, which owns the dropdown, its keyboard behaviour and its placement
 * and draws each action as its icon and label; destructive actions move to the end of the list there. No actions render nothing. An action with a `to` route is a real link (an anchor via
 * RouterLink), so middle click, Ctrl/Cmd click and "open in new tab" work and assistive technology announces a link.
 */
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiMenu from '@/components/ui/UiMenu.vue'
import { elementAttrs, isLink, toneClasses } from '@/utils/ui/menu'
import type { UiMenuAction } from '@/utils/ui/menu'

/** Props for UiRowActions. */
const props = defineProps<{
  /** The actions to show in the menu. */
  actions: UiMenuAction[]
  /** Per-row unique `data-testid` of the "…" trigger; `row-actions-trigger` when not given. */
  testid?: string
}>()

/** The only action of the row, shown as a plain button instead of a menu; null when there are none or several. */
const single = computed<UiMenuAction | null>(() => (props.actions.length === 1 ? props.actions[0] ?? null : null))

/** The `data-testid` of the "…" trigger: the caller's per-row id, or the shared default. */
const triggerTestid = computed<string>(() => props.testid ?? 'row-actions-trigger')

/** Look of the single action, drawn as an icon button. */
const ICON_BUTTON_CLASSES = 'inline-flex size-8 items-center justify-center rounded-control transition-colors hover:bg-surface-hover focus-visible:outline-2 focus-visible:outline-primary-500 disabled:cursor-not-allowed disabled:opacity-40'

/**
 * Activates the single action of a row; there is no menu to close.
 *
 * @param action - The row's only action.
 */
const selectSingle = (action: UiMenuAction): void => {
  if (action.disabled) { return }
  action.onSelect?.()
}
</script>

<template>
  <div v-if="single" class="inline-block" data-testid="row-actions">
    <!-- One action: the action itself, no menu; a link when it has a route -->
    <component
      :is="isLink(single) ? RouterLink : 'button'"
      v-bind="elementAttrs(single)"
      :data-testid="single.testid ?? 'row-actions-single'"
      :title="single.hint ?? single.label"
      :class="[ICON_BUTTON_CLASSES, toneClasses(single)]"
      @click="selectSingle(single)"
    >
      <UiIcon :markup="single.icon" size="sm" />
      <span class="sr-only">{{ single.label }}</span>
    </component>
  </div>
  <UiMenu
    v-else-if="actions.length > 1"
    :actions="actions"
    :testid="triggerTestid"
    menu-testid="row-actions-menu"
    data-testid="row-actions"
  />
</template>
