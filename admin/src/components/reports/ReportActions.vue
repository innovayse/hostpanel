<script setup lang="ts">
/**
 * Export and print buttons of a report, placed in the `actions` slot of the report header.
 *
 * Each button shows only when the screen switches it on and tells the screen when it is pressed.
 */
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'

const { t } = useI18n()

/** Props for ReportActions. */
withDefaults(defineProps<{
  /** Shows an Export CSV button. */
  exportable?: boolean
  /** Shows a Print button. */
  printable?: boolean
}>(), {
  exportable: false,
  printable: false,
})

const emit = defineEmits<{
  /** The operator asked for the CSV export. */
  export: []
  /** The operator asked to print. */
  print: []
}>()
</script>

<template>
  <div class="flex flex-wrap items-center gap-2">
    <UiButton v-if="exportable" variant="secondary" data-testid="report-export" @click="emit('export')">
      {{ t('shared.exportCsv') }}
    </UiButton>
    <UiButton v-if="printable" variant="secondary" data-testid="report-print" @click="emit('print')">
      {{ t('shared.print') }}
    </UiButton>
  </div>
</template>
