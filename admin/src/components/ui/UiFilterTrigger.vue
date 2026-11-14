<script setup lang="ts">
/**
 * The "Filters" button of a list and the surface that holds the filter fields.
 *
 * The button shows how many filters are active (a badge, hidden at zero). Pressing it opens the default slot, the
 * filter fields, as a centred dialog on a desktop and as a bottom sheet on a phone (`UiDialog` and `UiDrawer`,
 * so focus trap, Escape and scroll lock are theirs). The footer has Reset, Cancel and Apply: Apply emits `apply`
 * and closes; Reset emits `reset` and leaves the surface open so the reader can choose again; Cancel, Escape, the
 * backdrop and the close button emit `cancel` and close. The component holds no filter state; the screen's
 * draft does (`useFilterQuery`), and `UiFilterChips` shows what is applied.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiDrawer from '@/components/ui/UiDrawer.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import { useMediaQuery } from '@/composables/useMediaQuery'
import { Icon } from '@/lib/icons'
import { UI_BELOW_SM_QUERY } from '@/utils/ui/breakpoints'

const { t } = useI18n()

/** The button is the only root that takes the caller's attributes; the surface is teleported. */
defineOptions({ inheritAttrs: false })

/** Props for UiFilterTrigger. */
const props = withDefaults(defineProps<{
  /** How many filters are active; the badge shows it and is hidden at zero. */
  count: number
  /** Title of the surface (already translated); defaults to a translated "Filters". */
  title?: string
  /** Disables the button (a list that is still loading). */
  disabled?: boolean
}>(), {
  title: undefined,
  disabled: false,
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

/** Whether the screen is a phone, which swaps the dialog for a bottom sheet. */
const isPhone = useMediaQuery(UI_BELOW_SM_QUERY)

/** The surface component: a bottom sheet on a phone, a centred dialog otherwise. */
const surface = computed(() => (isPhone.value ? UiDrawer : UiDialog))

/** Props of the chosen surface: both take `open` and `title`; the sheet asks the drawer to become one. */
const surfaceProps = computed(() => {
  const shared = { open: open.value, title: props.title ?? t('ui.uiFilterTrigger.title') }
  return isPhone.value ? { ...shared, sheet: true } : { ...shared, size: 'md' as const }
})

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
    variant="secondary"
    type="button"
    aria-haspopup="dialog"
    :aria-expanded="open"
    :disabled="disabled"
    data-testid="filter-trigger"
    v-bind="$attrs"
    @click="open = true"
  >
    <UiIcon :markup="Icon.Filter" size="sm" />
    {{ t('ui.uiFilterTrigger.filters') }}
    <UiBadge v-if="count > 0" tone="primary" small data-testid="filter-trigger-count">
      {{ count }}
    </UiBadge>
  </UiButton>
  <component :is="surface" v-bind="surfaceProps" @close="onCancel">
    <slot />
    <template #footer>
      <UiButton variant="ghost" class="mr-auto" data-testid="filter-reset" @click="emit('reset')">
        {{ t('ui.uiFilterTrigger.reset') }}
      </UiButton>
      <UiButton variant="secondary" data-testid="filter-cancel" @click="onCancel">
        {{ t('ui.uiFilterTrigger.cancel') }}
      </UiButton>
      <UiButton variant="primary" data-testid="filter-apply" @click="onApply">
        {{ t('ui.uiFilterTrigger.apply') }}
      </UiButton>
    </template>
  </component>
</template>
