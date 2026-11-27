<script setup lang="ts">
/**
 * The "Save" button beside a single settings control on a page that has no save bar of its own (the plugin settings cards).
 *
 * Each plugin settings card saves one row at a time and renders the same button with
 * the same two states; this is that button, once. It is disabled until there is an unsaved edit.
 * It sits in the kit's form action row, so it is the primary action and, on a phone, as wide as its card.
 */
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiFormActions from '@/components/ui/UiFormActions.vue'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'

defineOptions({ inheritAttrs: false })

const { t } = useI18n()

/** Props for SettingSaveButton. */
defineProps<{
  /** Whether there is an unsaved edit to send. */
  dirty: boolean
  /** True while a save is in flight anywhere on the page. */
  saving?: boolean
}>()

const emit = defineEmits<{
  /** Emitted when the operator presses Save. */
  click: []
}>()
</script>

<template>
  <UiFormActions class="max-sm:w-full">
    <UiButton
      v-bind="$attrs"
      :action="UiActionKind.Save"
      :type="UiButtonType.Button"
      :variant="UiButtonVariant.Primary"
      :loading="saving"
      :disabled="!dirty"
      @click="emit('click')"
    >
      {{ t('shared.save') }}
    </UiButton>
  </UiFormActions>
</template>
