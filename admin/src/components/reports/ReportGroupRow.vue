<script setup lang="ts">
/**
 * Full-width heading row that starts a group inside a report table body.
 */
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'

/** Props for ReportGroupRow. */
defineProps<{
  /** Group name. */
  label: string
  /** Number of columns of the table the row spans. */
  colspan: number
}>()
</script>

<template>
  <UiTableRow data-testid="report-group-row">
    <UiTableCell :colspan="colspan" strong data-testid="report-group-label">
      {{ label }}
    </UiTableCell>
  </UiTableRow>
</template>
