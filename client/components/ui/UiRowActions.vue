<template>
  <UiPopover v-model="open" panel-class="w-56">
    <template #trigger="{ toggle }">
      <UiIconButton
        :icon="MoreHorizontal"
        :label="$t('common.actions')"
        aria-haspopup="menu"
        :aria-expanded="open"
        :testid="testid"
        @click="toggle"
      />
    </template>
    <template #default="{ close }">
      <UiButton
        v-for="action in actions"
        :key="action.key"
        variant="ghost"
        role="menuitem"
        full-width
        :to="action.to"
        class="!justify-start gap-3"
        :data-testid="`${testid}-${action.key}`"
        @click="run(action); close()"
      >
        <component :is="action.icon" :size="16" :stroke-width="2" aria-hidden="true" />
        {{ action.label }}
      </UiButton>
    </template>
  </UiPopover>
</template>

<script setup lang="ts">
/**
 * UiRowActions: the "..." menu of a table row, the one actions control of a list row.
 *
 * The trigger is an icon button; the menu lists each action with its icon and label. An action with a route is a link and
 * one without runs its handler, then the menu closes. The control sits in a row that may itself open a record, so a click
 * on it never reaches the row (see `UiTableRow`).
 */
import { ref } from 'vue'
import { MoreHorizontal } from 'lucide-vue-next'
import type { UiRowAction } from '~/types/uirowaction'

/** Props of UiRowActions. */
interface Props {
  /** The actions of the row, in menu order. */
  actions: UiRowAction[]
  /** `data-testid` of the trigger; each menu item adds `-{key}`. */
  testid: string
}

/** The props of the component. */
defineProps<Props>()

/** Whether the menu is open. */
const open = ref(false)

/**
 * Runs an action that has no route; a routed action is followed by its link.
 *
 * @param action - The chosen action.
 */
const run = (action: UiRowAction): void => {
  if (action.to === undefined) {
    void action.onSelect?.()
  }
}
</script>
