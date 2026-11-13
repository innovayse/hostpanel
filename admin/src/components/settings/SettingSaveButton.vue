<script setup lang="ts">
/**
 * The "Save" button beside a single settings control on a page that has no save bar of its own (the plugin settings cards).
 *
 * Each plugin settings card saves one row at a time and renders the same button with
 * the same two states; this is that button, once. It is disabled until there is an unsaved edit.
 */
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'

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
  <UiButton
    type="button"
    variant="primary"
    :loading="saving"
    :disabled="!dirty"
    @click="emit('click')"
  >
    {{ t('common.save') }}
  </UiButton>
</template>
