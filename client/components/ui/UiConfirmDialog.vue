<template>
  <UiDialog
    :model-value="open"
    size="sm"
    :closable="false"
    @update:model-value="emit('cancel')"
  >
    <div class="text-center" data-testid="ui-confirm-dialog">
      <div
        class="w-12 h-12 rounded-full flex items-center justify-center mx-auto mb-4 border"
        :class="isDanger ? 'bg-red-500/10 border-red-500/20' : 'bg-yellow-500/10 border-yellow-500/20'"
      >
        <component
          :is="isDanger ? Trash2 : AlertTriangle"
          :size="22"
          :stroke-width="2"
          :class="isDanger ? 'text-red-600 dark:text-red-400' : 'text-yellow-600 dark:text-yellow-400'"
        />
      </div>
      <h3 class="text-base font-bold text-gray-900 dark:text-white mb-2">{{ title }}</h3>
      <p v-if="description" class="text-sm text-gray-500 dark:text-gray-400 leading-relaxed">{{ description }}</p>
    </div>

    <div class="flex gap-3 mt-6">
      <UiButton
        class="flex-1"
        variant="outline"
        data-testid="ui-confirm-cancel"
        @click="emit('cancel')"
      >
        {{ cancelLabel || $t('common.cancel') }}
      </UiButton>
      <UiButton
        class="flex-1"
        :variant="isDanger ? 'danger' : 'primary'"
        :loading="loading"
        data-testid="ui-confirm-confirm"
        @click="emit('confirm')"
      >
        {{ confirmLabel || $t('common.confirm') }}
      </UiButton>
    </div>
  </UiDialog>
</template>

<script setup lang="ts">
/**
 * UiConfirmDialog: a small "are you sure?" dialog with a cancel and a confirm button.
 *
 * A thin wrapper over UiDialog kept because it is used all over the client area. `variant`
 * `danger` shows a bin icon and a red confirm button; any other value shows a warning icon.
 * Escape or a backdrop click emits `cancel`.
 */
import { computed } from 'vue'
import { Trash2, AlertTriangle } from 'lucide-vue-next'
import { UiConfirmVariant } from '~/utils/ui/enums'

/** Props of UiConfirmDialog. */
interface Props {
  /** Whether the dialog is open. */
  open: boolean
  /** Question shown in bold. */
  title: string
  /** Explanation under the title. */
  description?: string
  /** Confirm button text; defaults to the translated "Confirm". */
  confirmLabel?: string
  /** Cancel button text; defaults to the translated "Cancel". */
  cancelLabel?: string
  /** `danger` for destructive actions, `warning` otherwise. */
  variant?: `${UiConfirmVariant}`
  /** Puts the confirm button in its loading state. */
  loading?: boolean
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  description: undefined,
  confirmLabel: undefined,
  cancelLabel: undefined,
  variant: undefined,
  loading: false,
})

/** Emits the component raises. */
const emit = defineEmits<{
  /** The reader confirmed. */
  confirm: []
  /** The reader cancelled, pressed Escape or clicked the backdrop. */
  cancel: []
}>()

/** Whether the action is destructive. */
const isDanger = computed(() => props.variant === UiConfirmVariant.Danger)
</script>
