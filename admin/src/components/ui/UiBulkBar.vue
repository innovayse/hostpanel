<script setup lang="ts">
/**
 * Floating bulk action bar of a selectable list.
 *
 * Shown while `count` is above zero, centered at the bottom of the viewport (glass-strong, pill radius, overlay shadow, above
 * the home indicator, full width on phones) and teleported to `<body>`, so no card or scroll box clips it. It reads "× N selected":
 * the × clears the selection (`clear`), and so does Escape while the bar is shown. The optional `summary` slot holds a figure about
 * the selection (a total amount) and the default slot the action buttons (secondary ones and one primary). The bar is a
 * `role="toolbar"` named by `label`, and the count is a polite live region, so a screen reader hears the selection change.
 * The root is a Teleport, which cannot take attributes, so a caller's `data-testid` goes onto the toolbar.
 */
import { onBeforeUnmount, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiTransition from '@/components/ui/UiTransition.vue'
import { Icon } from '@/lib/icons'
import { Key } from '@/utils/ui/keys'
import { UiSize } from '@/utils/ui/size'
import { UiTransitionKind } from '@/utils/ui/transition'

defineOptions({ inheritAttrs: false })

const { t } = useI18n()

/** Props for UiBulkBar. */
const props = defineProps<{
  /** How many rows are selected; the bar is shown while it is above zero. */
  count: number
}>()

const emit = defineEmits<{
  /** Emitted when the user clears the selection (the × button or Escape). */
  clear: []
}>()

/**
 * Clears the selection on Escape, unless something else (an open dialog, a dropdown) already took the key.
 *
 * @param event - The keystroke.
 */
const onKeydown = (event: KeyboardEvent): void => {
  if (event.key === Key.Escape && props.count > 0 && !event.defaultPrevented) {
    emit('clear')
  }
}

onMounted(() => {
  window.addEventListener('keydown', onKeydown)
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKeydown)
})
</script>

<template>
  <Teleport to="body">
    <div class="pointer-events-none fixed inset-x-0 bottom-0 z-40 flex justify-center px-4 pb-[max(1rem,env(safe-area-inset-bottom))] max-sm:px-0 max-sm:pb-[env(safe-area-inset-bottom)]">
      <UiTransition :kind="UiTransitionKind.BulkBar">
        <div
          v-if="count > 0"
          v-bind="$attrs"
          role="toolbar"
          :aria-label="t('ui.bulkBar.label')"
          class="glass-strong shadow-overlay pointer-events-auto flex w-full flex-wrap items-center gap-3 rounded-pill px-3 py-2 sm:w-auto max-sm:rounded-panel"
        >
          <span class="flex items-center gap-1">
            <UiIconButton
              :label="t('ui.bulkBar.clear')"
              :size="UiSize.Sm"
              data-testid="ui-bulk-bar-clear"
              @click="emit('clear')"
            >
              <UiIcon :markup="Icon.Close" :size="UiSize.Sm" />
            </UiIconButton>
            <span class="text-sm font-medium text-text-primary" aria-live="polite" data-testid="ui-bulk-bar-count">
              {{ t('ui.bulkBar.selected', { count }, count) }}
            </span>
          </span>
          <span v-if="$slots.summary" class="text-sm text-text-secondary" data-testid="ui-bulk-bar-summary">
            <slot name="summary" />
          </span>
          <span class="flex flex-wrap items-center gap-2">
            <slot />
          </span>
        </div>
      </UiTransition>
    </div>
  </Teleport>
</template>
