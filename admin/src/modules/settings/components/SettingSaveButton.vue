<script setup lang="ts">
/**
 * The green "Save" button beside a single settings control.
 *
 * Every card on the System Settings page saves one row at a time and rendered
 * the same button with the same two states; this is that button, once.
 */
import { useI18n } from 'vue-i18n'

defineProps<{
  /** Whether there is an unsaved edit to send. */
  dirty: boolean
  /** True while a save is in flight anywhere on the page. */
  saving?: boolean
}>()

const emit = defineEmits<{ click: [] }>()

const { t } = useI18n()
</script>

<template>
  <button
    type="button"
    class="rounded-xl bg-status-green/15 border border-status-green/40 px-4 py-2 text-sm font-semibold text-status-green transition-colors hover:bg-status-green/25 disabled:cursor-not-allowed disabled:opacity-40"
    :disabled="!dirty || saving"
    @click="emit('click')"
  >
    {{ saving ? t('common.saving') : t('common.save') }}
  </button>
</template>
