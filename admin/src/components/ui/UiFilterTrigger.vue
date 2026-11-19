<script setup lang="ts">
/**
 * The "Filters" button of a list and the surface that holds the filter fields.
 *
 * The button shows how many filters are active (a badge, hidden at zero). Pressing it opens the default slot, the
 * filter fields in a `UiDialog`: a centred dialog on a desktop and, titled "Filters", a bottom sheet on a phone (the
 * dialog turns itself into one, so focus trap, Escape, drag down and scroll lock are the surface's). The footer has
 * Clear, Cancel (not on a phone, where the sheet has its close button, the backdrop and the drag) and Apply: Apply emits
 * `apply` and closes; Clear emits `reset` and leaves the surface open so the reader can choose again; Cancel, Escape, the
 * backdrop and the close button emit `cancel` and close. The component holds no filter state; the screen's
 * draft does (`useFilterQuery`), and `UiFilterChips` shows what is applied. A screen whose filters have nothing to clear
 * switches the Clear button off with `resettable`.
 */
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import { Icon } from '@/lib/icons'
import { UiActionKind } from '@/utils/ui/actions'
import { UiDialogSize } from '@/utils/ui/dialog'

const { t } = useI18n()

/** The button is the only root that takes the caller's attributes; the surface is teleported. */
defineOptions({ inheritAttrs: false })

/** Props for UiFilterTrigger. */
withDefaults(defineProps<{
  /** How many filters are active; the badge shows it and is hidden at zero. */
  count: number
  /** Title of the surface (already translated); defaults to a translated "Filters". */
  title?: string
  /** Disables the button (a list that is still loading). */
  disabled?: boolean
  /** Shows the Clear button. */
  resettable?: boolean
}>(), {
  title: undefined,
  disabled: false,
  resettable: true,
})

const emit = defineEmits<{
  /** Emitted when the reader presses Apply. */
  apply: []
  /** Emitted when the reader presses Reset. */
  reset: []
  /** Emitted when the reader cancels: the Cancel button, Escape, the backdrop or the close button. */
  cancel: []
}>()

/** Whether the filter surface is shown. */
const open = ref(false)

/**
 * Applies the draft and closes the surface.
 *
 * @returns Nothing; `apply` is emitted.
 */
const onApply = (): void => {
  emit('apply')
  open.value = false
}

/**
 * Cancels the draft and closes the surface.
 *
 * @returns Nothing; `cancel` is emitted.
 */
const onCancel = (): void => {
  emit('cancel')
  open.value = false
}
</script>

<template>
  <UiButton
    :icon="Icon.Filter"
    variant="secondary"
    type="button"
    aria-haspopup="dialog"
    :aria-expanded="open"
    :disabled="disabled"
    data-testid="filter-trigger"
    v-bind="$attrs"
    @click="open = true"
  >
    {{ t('ui.uiFilterTrigger.filters') }}
    <UiBadge v-if="count > 0" tone="primary" small data-testid="filter-trigger-count">
      {{ count }}
    </UiBadge>
  </UiButton>
  <UiDialog :open="open" :title="title ?? t('ui.uiFilterTrigger.title')" :size="UiDialogSize.Md" @close="onCancel">
    <slot />
    <template #footer>
      <UiButton v-if="resettable" :action="UiActionKind.Reset" variant="ghost" class="mr-auto" data-testid="filter-reset" @click="emit('reset')">
        {{ t('ui.uiFilterTrigger.reset') }}
      </UiButton>
      <UiButton :action="UiActionKind.Cancel" variant="secondary" class="max-sm:hidden" data-testid="filter-cancel" @click="onCancel">
        {{ t('ui.uiFilterTrigger.cancel') }}
      </UiButton>
      <UiButton :action="UiActionKind.Apply" variant="primary" data-testid="filter-apply" @click="onApply">
        {{ t('ui.uiFilterTrigger.apply') }}
      </UiButton>
    </template>
  </UiDialog>
</template>
